using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Middleware;
using Backend.Settings;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Backend.Processors;

var bld = WebApplication.CreateBuilder(args);
if (!bld.Environment.IsDevelopment() &&
    !bld.Environment.IsEnvironment("Testing") &&
    bld.Configuration["Auth:Jwt:Key"] == JwtSetting.PlaceholderKey)
{
    throw new InvalidOperationException("Auth:Jwt:Key must be supplied through secure configuration outside development and testing.");
}
var maximumPayloadSize = bld.Configuration.GetValue<long?>("Payload:MaximumSize") ?? 25 * 1024 * 1024;
var defaultConnection = bld.Configuration.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
var hangfireConnection = bld.Configuration["Hangfire:Storage:ConnectionString"] ?? defaultConnection;

bld.Services
   .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All)
   .SwaggerDocument();

bld.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        var webSetting = bld.Configuration.GetRequiredSection("Web").Get<WebSetting>()
                         ?? throw new InvalidOperationException("Web configuration is required.");
        builder.WithOrigins(webSetting.AllowedDomains())
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

bld.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(defaultConnection));

bld.Services
    .AddAuthenticationCookie(TimeSpan.FromMinutes(bld.Configuration.GetValue<int>("Auth:AccessTokenValidity")), options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = bld.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
    })
    .AddAuthenticationJwtBearer(x => x.SigningKey = bld.Configuration["Auth:Jwt:Key"])
    .AddAuthentication(o =>
   {
       o.DefaultScheme = "Jwt_Or_Cookie";
       o.DefaultAuthenticateScheme = "Jwt_Or_Cookie";
   })
   .AddPolicyScheme("Jwt_Or_Cookie", "Jwt_Or_Cookie", o =>
   {
       o.ForwardDefaultSelector = ctx =>
       {
           if (ctx.Request.Headers.TryGetValue(HeaderNames.Authorization, out var authHeader) &&
               authHeader.FirstOrDefault()?.StartsWith("Bearer ") is true)
           {
               return JwtBearerDefaults.AuthenticationScheme;
           }
           return CookieAuthenticationDefaults.AuthenticationScheme;
       };
   });
bld.Services.AddAuthorization();
bld.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters.ValidateIssuer = true;
    options.TokenValidationParameters.ValidIssuer = bld.Configuration["Auth:Jwt:Issuer"];
    options.TokenValidationParameters.ValidateAudience = true;
    options.TokenValidationParameters.ValidAudience = bld.Configuration["Auth:Jwt:Audience"];
});
bld.Services.AddHttpContextAccessor();
bld.Services.AddProblemDetails();
bld.Services.AddHealthChecks();
bld.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
bld.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? context.Connection.RemoteIpAddress?.ToString()
                  ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

// configure HanngFire
bld.Services.AddHangfire(config =>
    {
        config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
              .UseSimpleAssemblyNameTypeSerializer()
              .UseRecommendedSerializerSettings()
              .UsePostgreSqlStorage(options =>
                  options.UseNpgsqlConnection(hangfireConnection));
    });

bld.Services.AddHangfireServer();

// configure settings
bld.Services.AddOptions<PayloadSetting>()
    .Bind(bld.Configuration.GetRequiredSection("Payload"))
    .Validate(setting => setting.MaximumSize is > 0 and <= 1024L * 1024 * 1024, "Payload maximum size must be between 1 byte and 1 GB.")
    .ValidateOnStart();
bld.Services.AddOptions<WebSetting>()
    .Bind(bld.Configuration.GetRequiredSection("Web"))
    .Validate(setting => setting.AllowedDomains().Length > 0 && setting.AllowedDomains().All(domain => Uri.TryCreate(domain, UriKind.Absolute, out _)),
        "Web domains must be valid absolute URLs.")
    .ValidateOnStart();

// rely on middleware and FormOptions to enforce limits to avoid abrupt connection resets
bld.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maximumPayloadSize);
bld.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maximumPayloadSize);

// configure features
Helper.AddFeatures(bld.Services, bld.Configuration);


// configure services 
bld.Services.AddScoped<DataSeeder>();

var permissionProviders = typeof(Program).Assembly.GetTypes()
    .Where(t => typeof(IPermissionDefinitionProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
foreach (var provider in permissionProviders)
{
    bld.Services.AddScoped(typeof(IPermissionDefinitionProvider), provider);
}

bld.Services.AddScoped<IPermissionDefinitionService, PermissionDefinitionService>();

// Configure email services
bld.Services.Configure<EmailSetting>(bld.Configuration.GetSection("EmailSettings"));
bld.Services.AddScoped<IEmailService, EmailService>();
bld.Services.AddScoped<IEmailBackgroundJobs, EmailBackgroundJobs>();

var app = bld.Build();

// Run migrations and seed data
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var applyMigrationsOnStartup = bld.Configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup")
                               ?? (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"));
if (applyMigrationsOnStartup)
{
    dbContext.Database.Migrate();
}
var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
await seeder.SeedAsync();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors()
   .UseAuthentication()
   .UseMiddleware<SessionValidationMiddleware>()
   .UseRateLimiter()
   .UseAuthorization();

if (app.Environment.IsDevelopment())
    app.UseMiddleware<DevelopmentEndpointLoggingMiddleware>();

app.UseFastEndpoints(
       c =>
       {
           c.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
           c.Endpoints.Configurator = ep =>
           {
               ep.PreProcessor<ToLargePayloadProcessor>(Order.Before);
               ep.PostProcessor<ExceptionProcessor>(Order.After);
               ep.PostProcessor<UnsupportedMediaTypeResponseProcessor>(Order.After);
           };
           // c.Binding.ReflectionCache.AddFromBackend();
           c.Endpoints.RoutePrefix = bld.Configuration.GetRequiredSection("RoutePrefix").Value;
           c.Versioning.Prefix = "v";
           c.Security.RoleClaimType = ClaimTypes.Role;
           c.Security.PermissionsClaimType = ClaimConstants.Permission;
           c.Errors.UseProblemDetails(x =>
           {
               x.IndicateErrorCode = true;     //serializes the fluentvalidation error code
               x.TypeValue = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1";
               x.TitleValue = "One or more validation errors occurred.";
               x.TitleTransformer = pd => pd.Status switch
               {
                   400 => "Validation Error",
                   404 => "Not Found",
                   _ => "One or more errors occurred!"
               };
           });
       });

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

app.MapHealthChecks("/health").AllowAnonymous();

// Configure Hangfire dashboard after database is ready
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthorizationFilter()]
});

// Move recurring jobs setup after database is ready
using (app.Services.CreateScope())
{
    RecurringJob.AddOrUpdate<IAuthTokenCleanService>("delete-expired-auth-tokens", service => service.DeleteExpiredTokensAsync(), Cron.Daily);
    RecurringJob.AddOrUpdate<ITokenCleanService>("delete-expired-tokens", service => service.DeleteExpiredTokensAsync(), Cron.Daily);
}

app.Run();

namespace Backend
{
    /// <summary>
    /// Empty type marker declared so that the top-level statements host can be
    /// referenced from integration tests (for example <c>WebApplicationFactory&lt;Program&gt;</c>).
    /// </summary>
    public class Program { }
}

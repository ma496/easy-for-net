using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Backend.External.Email;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Localization.Core;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Push;
using Backend.Middleware;
using Backend.Settings;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Console;
using Backend.Processors;

var bld = WebApplication.CreateBuilder(args);
if (!bld.Environment.IsDevelopment() &&
    !bld.Environment.IsEnvironment("Testing") &&
    bld.Configuration["Auth:Jwt:Key"] == JwtSetting.PlaceholderKey)
{
    throw new InvalidOperationException("Auth:Jwt:Key must be supplied through secure configuration outside development and testing.");
}
// Sessions live in Redis everywhere but the test host, so a deployment without a connection string has
// nowhere to keep them. Refused here, at startup, rather than as a 503 on the first sign-in.
if (!bld.Environment.IsEnvironment("Testing") &&
    string.IsNullOrWhiteSpace(bld.Configuration.GetConnectionString("Redis")))
{
    throw new InvalidOperationException("ConnectionStrings:Redis is required outside testing: sessions are stored in Redis.");
}
// Tests run the host hundreds of times over and log nothing anybody reads, while every statement
// logged at Information is a SQL command formatted and written. Set here for the same reason as the
// rate limit below: appsettings.Testing.json is not in source control, so a setting there would be
// one machine's alone.
if (bld.Environment.IsEnvironment("Testing"))
{
    bld.Logging.SetMinimumLevel(LogLevel.Warning);
}
// A bearer client connecting to the notification hub passes its access token in the query string, and
// the hosting layer's "Request starting" line - the only log written with the query string in it, and
// written before any middleware could strip it - would record the token. Pinned here, in code, so no
// environment's appsettings brings it back to Information for every provider; the per-request
// Information lines it held are what the DevelopmentEndpointLoggingMiddleware and the endpoint logs
// already give without the query string. (A provider-specific rule, `Logging:Console:LogLevel:...`,
// would still outrank this, and must not be set to below Warning for this category.)
bld.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
// Every SQL command EF Core runs is an Information line several dozen lines long, which buries the
// one line per request. A more specific category in configuration still outranks this - setting
// `Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command` to Information brings the SQL back.
bld.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
if (bld.Environment.IsDevelopment())
{
    // One line per message, like the web app's dev server; warnings and errors keep their category.
    bld.Logging.AddConsoleFormatter<DevelopmentConsoleFormatter, ConsoleFormatterOptions>();
    bld.Logging.AddConsole(o => o.FormatterName = DevelopmentConsoleFormatter.FormatterName);
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

// Every IInterceptor a feature registers (a singleton, so EF builds one internal service provider for
// them all) is added to the context - the notifications slice's, which holds hub pushes raised inside a
// transaction until it commits, among them. Outside production a page taken from an unordered query
// fails rather than warns: rows split across pages unpredictably, and the test suite is what catches it.
var throwOnUnorderedPaging = bld.Environment.IsDevelopment() || bld.Environment.IsEnvironment("Testing");
bld.Services.AddDbContext<AppDbContext>((provider, options) =>
    options.UseNpgsql(defaultConnection)
           .AddInterceptors(provider.GetServices<IInterceptor>())
           .ConfigureWarnings(warnings =>
           {
               if (throwOnUnorderedPaging)
               {
                   warnings.Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning);
               }
           }));

bld.Services
    .AddAuthenticationCookie(TimeSpan.FromMinutes(bld.Configuration.GetValue<int>("Auth:AccessTokenValidity")), options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = bld.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;

        // Load-bearing, though not for revocation: the session a cookie names is read from the session
        // store on every request and access changes revoke it at once, whatever the cookie's age.
        // Expiring the cookie on the same clock as the access token (Auth:AccessTokenValidity) sends a
        // browser through the refresh endpoint like a bearer client. That is where the account is
        // re-examined (a deactivated account or stale SecurityStamp is refused, a suspended, deleted or
        // left tenant is dropped) and the session re-created, which is how changes that revoke nothing
        // (DataSeeder's startup reconciliation, the FeatureManagement configuration section) reach a
        // browser session. Sliding expiration would re-issue the cookie with the same session for as
        // long as somebody kept clicking.
        options.SlidingExpiration = false;
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
           // A WebSocket cannot carry a header from a browser-style client, so a bearer client of the
           // notification hub passes its token as ?access_token=. That is honoured on the hub's path alone;
           // anywhere else the parameter is ignored and the request falls through to the cookie.
           if (NotificationHubRegistration.CarriesQueryAccessToken(ctx.Request))
           {
               return JwtBearerDefaults.AuthenticationScheme;
           }
           return CookieAuthenticationDefaults.AuthenticationScheme;
       };
   });
bld.Services.AddAuthorization();

// Endpoint authorization is evaluated before the tenant enforcement point and against the permissions
// the request currently holds, so a caller whose tenant selection went stale is refused there - with a
// bare 403 - before the enforcement point can say why. This gives that refusal its reason.
bld.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationRefusalResultHandler>();
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
    // Only loopback proxies are trusted by default. Behind a reverse proxy in another container (the
    // production compose file) the proxy's address is not loopback, so its X-Forwarded-Proto would be
    // ignored and every request would look like plain HTTP. `ForwardedHeaders:TrustAllProxies` is for a
    // deployment whose API port is reachable only through that proxy - never one exposed directly.
    if (bld.Configuration.GetValue<bool>("ForwardedHeaders:TrustAllProxies"))
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});
// `RateLimit:PermitLimit` and `RateLimit:WindowMinutes` override these, and the Testing default is
// effectively no limit on purpose: permits are counted per identity, and a test suite is one
// identity making every request it can as fast as it can. Held at the production figure, a suite
// fast enough to be worth having trips the limiter and reports it as unrelated tests failing with
// 429. The default lives here rather than in appsettings.Testing.json because that file is not in
// source control - it is written per machine and per generated project - so a default set there
// would not be inherited by anybody.
var rateLimitPermits = bld.Configuration.GetValue<int?>("RateLimit:PermitLimit")
                       ?? (bld.Environment.IsEnvironment("Testing") ? int.MaxValue : 300);
var rateLimitWindow = TimeSpan.FromMinutes(bld.Configuration.GetValue<double?>("RateLimit:WindowMinutes") ?? 1);
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
            PermitLimit = rateLimitPermits,
            Window = rateLimitWindow,
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

// Storage, the dashboard and the recurring job registrations stay in every environment; only the
// worker is withheld from tests. Nothing under test waits for a job to be processed, and a worker
// polling the database throughout a run costs connections and attempts real deliveries - mail
// included - against settings that are placeholders outside a deployment.
if (!bld.Environment.IsEnvironment("Testing"))
{
    bld.Services.AddHangfireServer();
}

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
// the seeded administrators must be able to sign in, so their passwords meet the sign-in validator's bounds
bld.Services.AddOptions<SeedSetting>()
    .Bind(bld.Configuration.GetRequiredSection("Seed"))
    .Validate(setting => setting.PlatformAdminPassword is { Length: >= 8 and <= 50 } && setting.TenantAdminPassword is { Length: >= 8 and <= 50 },
        "Seed administrator passwords must be between 8 and 50 characters.")
    .ValidateOnStart();

// rely on middleware and FormOptions to enforce limits to avoid abrupt connection resets
bld.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maximumPayloadSize);
bld.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maximumPayloadSize);

// configure features
Helper.AddFeatures(bld.Services, bld.Configuration);

// The session store is the one registration that depends on the environment: Redis in every deployment,
// one shared in-memory store under Testing so the suite needs nothing but PostgreSQL. It is chosen here,
// where the environment is known, because a feature's AddServices receives only the configuration.
bld.Services.AddSessionStore(bld.Configuration, useInMemoryStore: bld.Environment.IsEnvironment("Testing"));

// The notification hub scales out through the same Redis, for the same reason chosen here: every
// deployment publishes through the backplane, so a push from any instance - or from the Hangfire job
// that raised it - reaches every connection; the test host is one process and runs without one.
bld.Services.AddNotificationHub(bld.Configuration, useBackplane: !bld.Environment.IsEnvironment("Testing"));

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
bld.Services.AddScoped<IEmailTransport, SmtpEmailTransport>();
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

// Placed ahead of every other middleware that could see a request's culture, and driven by
// Accept-Language alone (no query string or cookie provider) - a caller names its culture the one
// way HTTP already gives it. Supported cultures are exactly the shipped resource files, read off the
// same store the /localization/resources endpoint serves from, so an unshipped or absent header
// settles on English rather than failing the request.
var localizationResourceStore = app.Services.GetRequiredService<ILocalizationResourceStore>();
var supportedCultures = localizationResourceStore.ShippedCultures.Select(culture => new CultureInfo(culture)).ToList();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()],
});

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors()
   .UseAuthentication()
   .UseMiddleware<SessionStoreUnavailableMiddleware>()
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
               // the single tenant enforcement point: registered first, so it runs first - ahead of the
               // payload guard and every endpoint-level pre-processor - establishes the tenant the
               // request acts in, and refuses the request when a tenant-scoped endpoint has no usable
               // tenant to act in. Running it ahead of the payload guard also means a 413 answered below
               // is localized against the caller's own tenant overrides, not the platform's alone.
               ep.PreProcessor<TenantContextProcessor>(Order.Before);
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
           // The one place a ThrowError call or a validator rule's .WithErrorCode(ErrorCodes.X.Value)
           // is put into the request's culture: it runs right before the built-in ProblemDetails content
           // is written, on the same list of failures that content was just built from, so this is the
           // last chance to change what the caller sees. Every other response shape reaches this too
           // (a plain 200, a stream, ...), so only a ProblemDetails is touched - everything else passes
           // through unmodified. A plain FluentValidation rule with no ErrorCodes constant behind it
           // (NotEmptyValidator, ...) has no error.server.<code> key and is left exactly as written.
           // FastEndpoints' own SendErrorsAsync runs this too, which is what lets the permission refusal
           // AuthorizationRefusalResultHandler answers with - built inside UseAuthorization, ahead of
           // every FastEndpoints pre-processor including TenantContextProcessor - reach it; ErrorLocalization
           // covers that case by falling back to the session's own tenant_id claim when no scope was
           // established at all.
           c.Endpoints.GlobalResponseModifierAsync = async (httpContext, response) =>
           {
               if (response is not ProblemDetails problemDetails)
               {
                   return;
               }

               var localizationService = httpContext.RequestServices.GetRequiredService<IErrorMessageLocalizer>();
               foreach (var error in problemDetails.Errors)
               {
                   error.Reason = await localizationService.LocalizeErrorAsync(
                       httpContext, error.Code, error.Name, error.Reason, httpContext.RequestAborted);
               }

               // Detail was computed once at construction, from the error the framework built before
               // this ran - recomputed here so a single-error response's `detail` carries the same
               // localized text `errors` now does, exactly as FastEndpoints' own default would if the
               // reason had been correct from the start.
               if (problemDetails.Errors.Count() == 1)
               {
                   problemDetails.Detail = problemDetails.Errors.First().Reason;
               }
           };
       });

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

// In Development the body also names the checkout the host runs from, so a live check can tell this
// checkout's API from a stale one answering on the same port.
(app.Environment.IsDevelopment()
        ? app.MapHealthChecks("/health", DevelopmentHealthResponse.Options(app.Environment.ContentRootPath))
        : app.MapHealthChecks("/health"))
    .AllowAnonymous();

// Behind the same authentication and session validation as every endpoint above, and the same
// SessionStoreUnavailableMiddleware: the upgrade request is refused with 401 or 503 exactly as an HTTP
// request with the same credential would be.
app.MapNotificationHub();

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
    RecurringJob.AddOrUpdate<INotificationRetentionService>("delete-expired-notifications", service => service.DeleteExpiredAsync(CancellationToken.None), Cron.Daily);
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

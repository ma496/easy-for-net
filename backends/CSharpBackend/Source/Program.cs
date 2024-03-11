using Efn.Identity.Services;
using Efn.Infrastructure.EfPersistence;
using Efn.Infrastructure.EfPersistence.Interceptors;
using Efn.Infrastructure.Services;
using Efn.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services
   .AddFastEndpoints()
   .SwaggerDocument()
   .AddDbContext<AppDbContext>(opt => opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")))
   .AddCors(opt =>
   {
       opt.AddDefaultPolicy(builder =>
       {
           builder.AllowAnyHeader();
           builder.AllowAnyMethod();
           builder.AllowAnyOrigin();
       });
   })
   .AddHealthChecks()
   .AddDbContextCheck<AppDbContext>();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IDateTime, DateTimeService>();
builder.Services.AddScoped<AuditableEntitySaveChangesInterceptor>();
builder.Services.AddScoped<IUserManager, UserManager>();
builder.Services.AddScoped<IDataSeedManager, DataSeedManager>();

var app = builder.Build();
app
    .UseAuthentication()
    .UseAuthorization()
    .UseFastEndpoints(c =>
    {
        c.Endpoints.RoutePrefix = "api";
    })
    .UseSwaggerGen()
    .UseHttpsRedirection()
    .UseCors()
    .UseHealthChecks("/health");

await SeedData(app);

app.Run();

async Task SeedData(IHost app)
{
    var scopedFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

    using (var scope = scopedFactory.CreateScope())
    {
        var dataSeedManager = scope.ServiceProvider.GetRequiredService<IDataSeedManager>();
        await dataSeedManager.Seed();
    }
}

public partial class Program { }
using Efn.Infrastructure.EfPersistence;
using Efn.Infrastructure.EfPersistence.Interceptors;
using Efn.Infrastructure.Services;
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

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IDateTime, DateTimeService>();
builder.Services.AddScoped<AuditableEntitySaveChangesInterceptor>();

var app = builder.Build();
app.UseFastEndpoints()
   .UseSwaggerGen()
   .UseHttpsRedirection()
   .UseCors()
   .UseHealthChecks("/health");
app.Run();

public partial class Program { }
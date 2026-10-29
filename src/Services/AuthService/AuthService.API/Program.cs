using AuthService.API.Configuration;
using AuthService.API.Middleware;
using AuthService.Business.Configuration;
using AuthService.Business.Interfaces;
using AuthService.Business.Security;
using AuthService.Business.Services;
using AuthService.DataAccess.Context;
using AuthService.DataAccess.Entities;
using AuthService.DataAccess.Repositories;
using AuthService.DataAccess.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("AuthDb")
    ?? throw new InvalidOperationException(
        "Connection string 'AuthDb' is not configured. Load .env.local before starting the service.");

builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are missing.");

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer)
    || string.IsNullOrWhiteSpace(jwtSettings.Audience)
    || string.IsNullOrWhiteSpace(jwtSettings.SigningKey)
    || jwtSettings.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt__Issuer, Jwt__Audience and a Jwt__SigningKey of at least 32 characters are required.");
}

var internalApiSettings = builder.Configuration.GetSection("InternalApi").Get<InternalApiSettings>()
    ?? throw new InvalidOperationException("Internal API settings are missing.");

if (string.IsNullOrWhiteSpace(internalApiSettings.Key) || internalApiSettings.Key.Length < 32)
{
    throw new InvalidOperationException("InternalApi__Key of at least 32 characters is required.");
}

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(internalApiSettings);
builder.Services.AddSingleton<JwtTokenGenerator>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();

builder.Services.AddJwtAuthentication(jwtSettings);
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Auth Service API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    var bootstrapSettings = builder.Configuration.GetSection("Bootstrap").Get<BootstrapOwnerSettings>()
        ?? throw new InvalidOperationException("Bootstrap settings are missing.");

    if (string.IsNullOrWhiteSpace(bootstrapSettings.OwnerEmail)
        || string.IsNullOrWhiteSpace(bootstrapSettings.OwnerPassword))
    {
        throw new InvalidOperationException(
            "Bootstrap__OwnerEmail and Bootstrap__OwnerPassword are required in Development.");
    }

    using var scope = app.Services.CreateScope();
    await AuthDataSeeder.SeedAsync(scope.ServiceProvider, bootstrapSettings);
}

app.Run();

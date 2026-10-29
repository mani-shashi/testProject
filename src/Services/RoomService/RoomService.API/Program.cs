using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RoomService.API.Configuration;
using RoomService.API.Middleware;
using RoomService.API.Security;
using RoomService.Business.Interfaces;
using RoomService.Business.Services;
using RoomService.DataAccess.Context;
using RoomService.DataAccess.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("RoomDb")
    ?? throw new InvalidOperationException(
        "Connection string 'RoomDb' is not configured. Load .env.local before starting the service.");

builder.Services.AddDbContext<RoomDbContext>(options => options.UseSqlServer(connectionString));

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

var authBaseUrl = builder.Configuration["Services:AuthBaseUrl"]
    ?? throw new InvalidOperationException(
        "Services__AuthBaseUrl is required so token revocation can be checked against the Auth Service.");

if (!Uri.TryCreate(authBaseUrl, UriKind.Absolute, out var authBaseUri))
{
    throw new InvalidOperationException("Services__AuthBaseUrl must be an absolute URL.");
}

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(internalApiSettings);
builder.Services.AddHttpClient<TokenStatusValidator>(client =>
{
    client.BaseAddress = authBaseUri;
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<IRoomTypeRepository, RoomTypeRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IRoomRateRepository, RoomRateRepository>();
builder.Services.AddScoped<IRoomBlockRepository, RoomBlockRepository>();

builder.Services.AddScoped<IRoomTypeService, RoomTypeService>();
builder.Services.AddScoped<IRoomManagementService, RoomManagementService>();
builder.Services.AddScoped<IRoomRateService, RoomRateService>();
builder.Services.AddScoped<IRoomBlockService, RoomBlockService>();

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
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Room Service API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

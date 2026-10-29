using Microsoft.OpenApi;
using RoomService.API.Middleware;

namespace RoomService.API.Configuration;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Room Service API",
                Version = "v1",
                Description = "Room types, rooms, rates, maintenance blocks and physical availability."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token returned by the Auth Service."
            });

            options.AddSecurityDefinition("InternalApiKey", new OpenApiSecurityScheme
            {
                Name = InternalApiKeyMiddleware.HeaderName,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Service-to-service key required by /internal endpoints. Not needed for public endpoints."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", document),
                    new List<string>()
                }
            });
        });

        return services;
    }
}

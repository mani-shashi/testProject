using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Business.Configuration;
using AuthService.Business.Interfaces;
using AuthService.Business.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.API.Configuration;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, JwtSettings settings)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.Email
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var stamp = context.Principal?.FindFirstValue(JwtTokenGenerator.SecurityStampClaim);

                        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(stamp))
                        {
                            context.Fail("Token is missing required claims.");
                            return;
                        }

                        var status = await users.GetTokenStatusAsync(userId, context.HttpContext.RequestAborted);
                        if (status is null
                            || !status.IsActive
                            || !string.Equals(status.SecurityStamp, stamp, StringComparison.Ordinal))
                        {
                            context.Fail("The account is inactive or the token has been revoked.");
                        }
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}

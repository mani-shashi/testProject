using System.Security.Cryptography;
using System.Text;
using AuthService.API.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Middleware;

public sealed class InternalApiKeyMiddleware
{
    public const string HeaderName = "X-Internal-Api-Key";

    private readonly RequestDelegate _next;
    private readonly InternalApiSettings _settings;
    private readonly ILogger<InternalApiKeyMiddleware> _logger;

    public InternalApiKeyMiddleware(RequestDelegate next, InternalApiSettings settings, ILogger<InternalApiKeyMiddleware> logger)
    {
        _next = next;
        _settings = settings;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/internal"))
        {
            await _next(context);
            return;
        }

        if (IsValidKey(context.Request.Headers[HeaderName].ToString()))
        {
            await _next(context);
            return;
        }

        _logger.LogWarning("Rejected internal request to {Path}: missing or invalid API key.", context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "A valid internal API key is required.",
            Instance = context.Request.Path
        });
    }

    private bool IsValidKey(string provided)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(_settings.Key))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(_settings.Key);

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}

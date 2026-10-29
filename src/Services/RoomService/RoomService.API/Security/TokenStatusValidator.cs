using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoomService.API.Configuration;

namespace RoomService.API.Security;

public sealed class TokenStatusValidator
{
    public const string StampClaim = "stamp";

    private readonly HttpClient _httpClient;
    private readonly InternalApiSettings _internalApiSettings;
    private readonly ILogger<TokenStatusValidator> _logger;

    public TokenStatusValidator(
        HttpClient httpClient,
        InternalApiSettings internalApiSettings,
        ILogger<TokenStatusValidator> logger)
    {
        _httpClient = httpClient;
        _internalApiSettings = internalApiSettings;
        _logger = logger;
    }

    public async Task<bool> IsValidAsync(string userId, string stamp, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/internal/users/{Uri.EscapeDataString(userId)}/token-status");
        request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", _internalApiSettings.Key);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Auth token-status check for {UserId} failed with status {StatusCode}.",
                    userId,
                    (int)response.StatusCode);
                return false;
            }

            var status = await response.Content.ReadFromJsonAsync<TokenStatusResponse>(cancellationToken);
            return status is { IsActive: true }
                && string.Equals(status.SecurityStamp, stamp, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "Auth token-status check for {UserId} could not be completed.", userId);
            return false;
        }
    }

    private sealed record TokenStatusResponse(bool IsActive, string SecurityStamp);
}

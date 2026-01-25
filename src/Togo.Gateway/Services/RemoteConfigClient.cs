using System.Text.Json;
using Togo.Gateway.Configuration;

namespace Togo.Gateway.Services;

public class RemoteConfigClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RemoteConfigClient> _logger;

    public RemoteConfigClient(HttpClient httpClient, ILogger<RemoteConfigClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RemoteConfigModel?> FetchConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Replace with actual configuration URL
            var response = await _httpClient.GetAsync("https://api.internal.service/config", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<RemoteConfigModel>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch remote configuration.");
            return null; // Return null to signal failure logic
        }
    }
}

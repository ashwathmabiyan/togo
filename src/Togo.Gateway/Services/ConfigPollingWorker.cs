using Togo.Gateway.Configuration;

namespace Togo.Gateway.Services;

public class ConfigPollingWorker : BackgroundService
{
    private readonly RemoteConfigClient _client;
    private readonly InMemoryConfigProvider _configProvider;
    private readonly ILogger<ConfigPollingWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public ConfigPollingWorker(
        RemoteConfigClient client,
        InMemoryConfigProvider configProvider,
        ILogger<ConfigPollingWorker> logger)
    {
        _client = client;
        _configProvider = configProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var newConfig = await _client.FetchConfigAsync(stoppingToken);
                
                if (newConfig != null)
                {
                    _logger.LogInformation("Creating new YARP configuration from remote source.");
                    _configProvider.Update(newConfig.Routes, newConfig.Clusters);
                }
                else
                {
                    _logger.LogWarning("Using existing configuration due to fetch failure.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in config polling loop.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}

namespace PR_lab3_MemoryScramble.API.TaskSchedulers;

/// <summary>
/// Background service that periodically calls the /health endpoint to keep the Render instance awake.
/// Only active in the "Host" environment.
/// </summary>
public class HealthCheckScheduler : BackgroundService
{
    private readonly ILogger<HealthCheckScheduler> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _healthCheckInterval;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates a new HealthCheckScheduler.
    /// </summary>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="configuration">Application configuration to get the base URL</param>
    /// <param name="httpClientFactory">Factory to create HTTP clients</param>
    /// <param name="healthCheckInterval">Time between health checks (default: 13 minutes)</param>
    public HealthCheckScheduler(
        ILogger<HealthCheckScheduler> logger, 
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        TimeSpan? healthCheckInterval = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _healthCheckInterval = healthCheckInterval ?? TimeSpan.FromMinutes(13);
        _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
    }

    /// <summary>
    /// Executes the background task that calls the health endpoint periodically.
    /// </summary>
    /// <param name="stoppingToken">Token to signal when the service should stop</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HealthCheckScheduler started. Will ping /health every {Interval}", _healthCheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_healthCheckInterval, stoppingToken);
                
                if (!stoppingToken.IsCancellationRequested)
                {
                    await PerformHealthCheck(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is stopping
                _logger.LogInformation("HealthCheckScheduler stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during health check");
                // Continue running even if health check fails
            }
        }

        _logger.LogInformation("HealthCheckScheduler stopped");
    }

    /// <summary>
    /// Performs the actual health check by calling the /health endpoint.
    /// </summary>
    private async Task PerformHealthCheck(CancellationToken cancellationToken)
    {
        try
        {
            // Get the base URL from appsettings
            var baseUrl = _configuration["BaseUrl"] ?? "http://localhost:5253";
            var healthUrl = $"{baseUrl}/health";

            _logger.LogInformation("Performing health check at {Url}", healthUrl);

            var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Health check successful - Status: {StatusCode}", response.StatusCode);
            }
            else
            {
                _logger.LogWarning("Health check returned non-success status - Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during health check");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Health check request timed out");
        }
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}

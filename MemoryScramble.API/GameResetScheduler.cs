namespace PR_lab3_MemoryScramble.API;

/// <summary>
/// Background service that periodically resets the game board.
/// Only active in the "Host" environment.
/// </summary>
public class GameResetScheduler : BackgroundService
{
    private readonly Board _board;
    private readonly ILogger<GameResetScheduler> _logger;
    private readonly TimeSpan _resetInterval;

    /// <summary>
    /// Creates a new GameResetScheduler.
    /// </summary>
    /// <param name="board">The board to reset</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="resetInterval">Time between resets (default: 5 minutes)</param>
    public GameResetScheduler(Board board, ILogger<GameResetScheduler> logger, TimeSpan? resetInterval = null)
    {
        _board = board ?? throw new ArgumentNullException(nameof(board));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resetInterval = resetInterval ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Executes the background task that resets the board periodically.
    /// </summary>
    /// <param name="stoppingToken">Token to signal when the service should stop</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameResetScheduler started. Board will reset every {Interval}", _resetInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_resetInterval, stoppingToken);
                
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Resetting game board...");
                    await _board.Reset();
                    _logger.LogInformation("Game board reset completed");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is stopping
                _logger.LogInformation("GameResetScheduler stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while resetting board");
                // Continue running even if reset fails
            }
        }

        _logger.LogInformation("GameResetScheduler stopped");
    }
}

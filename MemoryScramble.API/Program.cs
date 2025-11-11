using PR_lab3_MemoryScramble.API;
using PR_lab3_MemoryScramble.API.TaskSchedulers;

var builder = WebApplication.CreateBuilder(args);

// Register services for dependency injection
builder.Services.AddCors();        // Enable Cross-Origin Resource Sharing
builder.Services.AddHttpClient();  // Register HTTP client factory for making external requests

var app = builder.Build();

// Get board file from appsettings.json or use default
// Priority: command-line args > appsettings > default "10x10.txt"
var boardFile = app.Configuration["BoardFile"] ?? "10x10.txt";
var boardFilePath = args.Length > 0 ? args[0] : $"Boards/{boardFile}";
var board = await Board.ParseFromFile(boardFilePath);


if (app.Environment.EnvironmentName == "Host")
{
    // Redirect HTTP to HTTPS in production
    app.UseHttpsRedirection();

    // Configure and start GameResetScheduler
    // Automatically resets the board at regular intervals to clear player state
    var resetIntervalMinutes = app.Configuration.GetValue<int>("GameResetIntervalMinutes", 5);

    var resetLogger = app.Services.GetRequiredService<ILogger<GameResetScheduler>>();
    var gameResetScheduler = new GameResetScheduler(board, resetLogger, TimeSpan.FromMinutes(resetIntervalMinutes));
    _ = gameResetScheduler.StartAsync(app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation("GameResetScheduler enabled in Host environment (resets every {Minutes} minutes)", resetIntervalMinutes);

    // Configure and start HealthCheckScheduler
    // Prevents cloud hosting platforms (e.g., Render) from putting the instance to sleep
    var healthIntervalMinutes = app.Configuration.GetValue<int>("HealthCheckIntervalMinutes", 13);

    var healthLogger = app.Services.GetRequiredService<ILogger<HealthCheckScheduler>>();
    var configuration = app.Services.GetRequiredService<IConfiguration>();
    var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
    var healthCheckScheduler = new HealthCheckScheduler(healthLogger, configuration, httpClientFactory, TimeSpan.FromMinutes(healthIntervalMinutes));
    _ = healthCheckScheduler.StartAsync(app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation("HealthCheckScheduler enabled in Host environment (pings /health every {Minutes} minutes)", healthIntervalMinutes);
}


/// <summary>
/// GET /look/{playerId}
/// Returns the current state of the board as seen by the specified player.
/// Cards controlled by the player are marked as 'my', face-up cards are 'up',
/// face-down cards are 'down', and empty spaces are 'none'.
/// </summary>
/// <param name="playerId">Unique identifier for the player</param>
/// <returns>Text representation of the board state (rows x cols format)</returns>
/// <response code="200">Successfully retrieved board state</response>
/// <response code="409">Invalid player ID or error occurred</response>
app.MapGet("/look/{playerId}", async (string playerId) =>
{
    // Validate player ID
    if (string.IsNullOrWhiteSpace(playerId))
        return Results.Text("Invalid player ID", "text/plain", statusCode: 409);

    try
    {
        // Get board state for this player
        var boardState = await Commands.Look(board, playerId);
        return Results.Text(boardState, "text/plain");
    }
    catch (Exception ex)
    {
        return Results.Text($"Error: {ex.Message}", "text/plain", statusCode: 409);
    }
});

/// <summary>
/// GET /flip/{playerId}/{location}
/// Flips a card at the specified location. If the player controls another card,
/// waits for it to be released first. If two matching cards are flipped, they are removed.
/// </summary>
/// <param name="playerId">Unique identifier for the player</param>
/// <param name="location">Card location in format "row,column" (0-indexed)</param>
/// <returns>Updated board state after flip operation</returns>
/// <response code="200">Successfully flipped card</response>
/// <response code="409">Invalid parameters, card unavailable, or error occurred</response>
app.MapGet("/flip/{playerId}/{location}", async (string playerId, string location) =>
{
    // Validate player ID
    if (string.IsNullOrWhiteSpace(playerId))
        return Results.Text("Invalid player ID", "text/plain", statusCode: 409);

    // Parse and validate location format (expected: "row,column")
    var parts = location.Split(',');
    if (parts.Length != 2 || !int.TryParse(parts[0], out int row) || !int.TryParse(parts[1], out int column))
        return Results.Text("Invalid location format", "text/plain", statusCode: 409);

    try
    {
        // Attempt to flip the card (may wait if player controls another card)
        var boardState = await Commands.Flip(board, playerId, row, column);
        return Results.Text(boardState, "text/plain");
    }
    catch (Exception ex)
    {
        return Results.Text($"cannot flip this card: {ex.Message}", "text/plain", statusCode: 409);
    }
});

/// <summary>
/// GET /replace/{playerId}/{fromCard}/{toCard}
/// Replaces all cards matching 'fromCard' with 'toCard'. Only affects cards
/// controlled by this player or face-up cards.
/// </summary>
/// <param name="playerId">Unique identifier for the player</param>
/// <param name="fromCard">Card value to search for (e.g., "🎮")</param>
/// <param name="toCard">New card value to replace with (e.g., "🎯")</param>
/// <returns>Updated board state after replacement</returns>
/// <response code="200">Successfully replaced cards</response>
/// <response code="409">Invalid parameters or error occurred</response>
app.MapGet("/replace/{playerId}/{fromCard}/{toCard}", async (string playerId, string fromCard, string toCard) =>
{
    // Validate all required parameters
    if (string.IsNullOrWhiteSpace(playerId) ||
        string.IsNullOrWhiteSpace(fromCard) ||
        string.IsNullOrWhiteSpace(toCard))
        return Results.Text("Invalid parameters", "text/plain", statusCode: 409);

    try
    {
        // Apply map operation to replace matching cards
        var boardState = await Commands.Map(
            board,
            playerId,
            async card => card == fromCard ? toCard : card);

        return Results.Text(boardState, "text/plain");
    }
    catch (Exception ex)
    {
        return Results.Text($"Error: {ex.Message}", "text/plain", statusCode: 409);
    }
});

/// <summary>
/// GET /watch/{playerId}
/// Long-polling endpoint that blocks until the board state changes, then returns
/// the updated state. Used for real-time updates without constant polling.
/// </summary>
/// <param name="playerId">Unique identifier for the player</param>
/// <returns>Updated board state when a change occurs</returns>
/// <response code="200">Board state changed, returning new state</response>
/// <response code="409">Invalid player ID or error occurred</response>
app.MapGet("/watch/{playerId}", async (string playerId) =>
{
    // Validate player ID
    if (string.IsNullOrWhiteSpace(playerId))
        return Results.Text("Invalid player ID", "text/plain", statusCode: 409);

    try
    {
        // Wait for board change and return updated state
        var boardState = await Commands.Watch(board, playerId);
        return Results.Text(boardState, "text/plain");
    }
    catch (Exception ex)
    {
        return Results.Text($"Error: {ex.Message}", "text/plain", statusCode: 409);
    }
});

/// <summary>
/// GET /health
/// Health check endpoint for monitoring and keep-alive purposes.
/// Returns "OK" if the service is running.
/// </summary>
/// <returns>Plain text "OK"</returns>
/// <response code="200">Service is healthy</response>
app.MapGet("/health", () => Results.Text("OK", "text/plain"));


// Enable CORS for all origins (allows web clients from any domain to access the API)
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Serve default files (e.g., index.html) for the root path
app.UseDefaultFiles();

// Serve static files from wwwroot folder (HTML, CSS, JS)
app.UseStaticFiles();


app.Run();

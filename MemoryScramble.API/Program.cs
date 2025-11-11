using PR_lab3_MemoryScramble.API;
using PR_lab3_MemoryScramble.API.TaskSchedulers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();
builder.Services.AddHttpClient();

var app = builder.Build();

// Get board file from appsettings
var boardFile = app.Configuration["BoardFile"] ?? "10x10.txt";
var boardFilePath = args.Length > 0 ? args[0] : $"Boards/{boardFile}";
var board = await Board.ParseFromFile(boardFilePath);

if (app.Environment.EnvironmentName == "Host")
{
    // Get reset interval from appsettings (default: 5 minutes)
    var resetIntervalMinutes = app.Configuration.GetValue<int>("GameResetIntervalMinutes", 5);
    
    // Start GameResetScheduler
    var resetLogger = app.Services.GetRequiredService<ILogger<GameResetScheduler>>();
    var gameResetScheduler = new GameResetScheduler(board, resetLogger, TimeSpan.FromMinutes(resetIntervalMinutes));
    _ = gameResetScheduler.StartAsync(app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation("GameResetScheduler enabled in Host environment (resets every {Minutes} minutes)", resetIntervalMinutes);
    
    // Get health check interval from appsettings (default: 13 minutes)
    var healthIntervalMinutes = app.Configuration.GetValue<int>("HealthCheckIntervalMinutes", 13);
    
    // Start HealthCheckScheduler
    var healthLogger = app.Services.GetRequiredService<ILogger<HealthCheckScheduler>>();
    var configuration = app.Services.GetRequiredService<IConfiguration>();
    var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
    var healthCheckScheduler = new HealthCheckScheduler(healthLogger, configuration, httpClientFactory, TimeSpan.FromMinutes(healthIntervalMinutes));
    _ = healthCheckScheduler.StartAsync(app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation("HealthCheckScheduler enabled in Host environment (pings /health every {Minutes} minutes)", healthIntervalMinutes);
}

app.MapGet("/look/{playerId}", async (string playerId) =>
{
    if (string.IsNullOrWhiteSpace(playerId))
        return Results.Text("Invalid player ID", "text/plain", statusCode: 409);

    try
    {
        var boardState = await Commands.Look(board, playerId);
        return Results.Text(boardState, "text/plain");
    }
    catch (Exception ex)
    {
        return Results.Text($"Error: {ex.Message}", "text/plain", statusCode: 409);
    }
});


app.MapGet("/flip/{playerId}/{location}", async (string playerId, string location) =>
{
    if (string.IsNullOrWhiteSpace(playerId))
        return Results.Text("Invalid player ID", "text/plain", statusCode: 409);

    var parts = location.Split(',');
    if (parts.Length != 2 || !int.TryParse(parts[0], out int row) || !int.TryParse(parts[1], out int column))
        return Results.Text("Invalid location format", "text/plain", statusCode: 409);

    try
    {
        var boardState = await Commands.Flip(board, playerId, row, column);
        return Results.Text(boardState, "text/plain");
    }
    catch (Exception ex)
    {
        return Results.Text($"cannot flip this card: {ex.Message}", "text/plain", statusCode: 409);
    }
});

app.MapGet("/replace/{playerId}/{fromCard}/{toCard}", async (string playerId, string fromCard, string toCard) =>
{
    if (string.IsNullOrWhiteSpace(playerId) ||
        string.IsNullOrWhiteSpace(fromCard) ||
        string.IsNullOrWhiteSpace(toCard))
        return Results.Text("Invalid parameters", "text/plain", statusCode: 409);

    try
    {
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

app.MapGet("/watch/{playerId}", async (string playerId) =>
{
    if (string.IsNullOrWhiteSpace(playerId))
        return Results.Text("Invalid player ID", "text/plain", statusCode: 409);

    try
    {
        var boardState = await Commands.Watch(board, playerId);
        return Results.Text(boardState, "text/plain");
    }
    catch (Exception ex)
    {
        return Results.Text($"Error: {ex.Message}", "text/plain", statusCode: 409);
    }
});

app.MapGet("/health", () => Results.Text("OK", "text/plain"));


app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseStaticFiles();


app.Run();

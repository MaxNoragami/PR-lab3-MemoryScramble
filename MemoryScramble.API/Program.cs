using PR_lab3_MemoryScramble.API;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();

var app = builder.Build();

var boardFilePath = args.Length > 0 ? args[0] : "Boards/test.txt";
var board = await Board.ParseFromFile(boardFilePath);


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

app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseStaticFiles();


app.Run();

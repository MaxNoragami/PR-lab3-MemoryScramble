using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.Simulation;

public class Simulation
{
    public static async Task Main(string[] args)
        => await SimulationMain();

    private static async Task SimulationMain()
    {
        const string filename = "SimulationBoards/test.txt";
        var board = await Board.ParseFromFile(filename);

        int rows = board.Rows;
        int columns = board.Columns;

        const int players = 1;
        const int tries = 30;

        var playerTasks = new List<Task>();
        for (int i = 0; i < players; i++)
            playerTasks.Add(Player(board, i, rows, columns, tries));

        await Task.WhenAll(playerTasks);
    }

    private static async Task Player(Board board, int playerNumber, int rows, int columns, int tries)
    {
        var playerId = $"player{playerNumber}";
        
        Console.WriteLine($"[{playerId}] Starting simulation with {tries} attempts");
        Console.WriteLine($"Initial board:\n{board}");

        for (int attempt = 0; attempt < tries; attempt++)
        {
            try
            {
                var firstRow = RandomInt(rows);
                var firstCol = RandomInt(columns);

                Console.WriteLine($"\n[{playerId}] Attempt {attempt + 1}: Flipping first card at ({firstRow}, {firstCol})");
                var resultAfterFirst = await Commands.Flip(board, playerId, firstRow, firstCol);
                Console.WriteLine($"After first flip:\n{resultAfterFirst}");

                var secondRow = RandomInt(rows);
                var secondCol = RandomInt(columns);
                
                Console.WriteLine($"[{playerId}] Attempt {attempt + 1}: Flipping second card at ({secondRow}, {secondCol})");
                var resultAfterSecond = await Commands.Flip(board, playerId, secondRow, secondCol);
                Console.WriteLine($"After second flip:\n{resultAfterSecond}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{playerId}] Attempt {attempt + 1} failed: {ex.Message}");
            }
        }

        Console.WriteLine($"\n[{playerId}] Simulation complete");
        Console.WriteLine($"Final board:\n{board}");
    }

    private static int RandomInt(int max)
        => Random.Shared.Next(max);
}

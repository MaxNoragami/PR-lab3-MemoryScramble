using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.Simulation;

public class Simulation
{
    public static async Task Main(string[] args)
    {
        // Flags (no leading dashes)
        bool infinite = args.Any(a => string.Equals(a, "infinite", StringComparison.OrdinalIgnoreCase));
        bool noDelay  = args.Any(a => string.Equals(a, "no-delay", StringComparison.OrdinalIgnoreCase));

        // First non-flag arg is treated as a path
        string? path = args.FirstOrDefault(a =>
            !string.Equals(a, "infinite", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "no-delay", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(a));

        // Build board
        Board board;
        if (!string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine($"Loading board from file: {path}");
            board = await Board.ParseFromFile(path);
        }
        else
        {
            // RANDOM MODE: generate an in-memory board (letters only, not saved)
            var rows = Random.Shared.Next(1, 51); // 1..50
            var cols = Random.Shared.Next(1, 51); // 1..50

            var letters = Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToArray();
            var k = Random.Shared.Next(3, 28); // 3..27 distinct letters
            var selected = letters.OrderBy(_ => Random.Shared.Next()).Take(k).ToArray();

            var lines = GenerateLetterBoardLines(rows, cols, selected);
            board = Board.ParseFromLines(lines);

            Console.WriteLine($"Generated {rows}x{cols} in-memory board with {k} letters (A–Z only).");
        }

        // Simulation config
        const int players = 8;
        const int tries   = 80; // ignored if infinite==true

        Console.WriteLine($"\nStarting simulation with {players} concurrent players"
                          + (infinite ? " (infinite mode: press Ctrl+C to stop)" : ""));
        Console.WriteLine($"Initial board:\n{board}\n");

        var playerTasks = new List<Task>();
        for (int i = 0; i < players; i++)
        {
            var startDelay = noDelay ? 1 : Random.Shared.Next(0, 100);
            playerTasks.Add(Player(board, i, board.Rows, board.Columns, tries, startDelay, noDelay, infinite));
        }

        await Task.WhenAll(playerTasks);

        Console.WriteLine($"\n\n=== SIMULATION {(infinite ? "STOPPED" : "COMPLETE")} ===");
        Console.WriteLine($"Final board:\n{board}");
    }

    private static string[] GenerateLetterBoardLines(int rows, int columns, string[] alphabetSubset)
    {
        var total = rows * columns;
        var pairs = total / 2;

        // Build pairs cycling through the chosen subset
        var cards = new List<string>(total);
        for (int i = 0; i < pairs; i++)
        {
            var letter = alphabetSubset[i % alphabetSubset.Length];
            cards.Add(letter);
            cards.Add(letter);
        }

        // If odd cells, add a random single
        if ((total % 2) == 1)
            cards.Add(alphabetSubset[Random.Shared.Next(alphabetSubset.Length)]);

        // Shuffle
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        // Compose lines: header + one card per line
        var lines = new string[1 + cards.Count];
        lines[0] = $"{rows}x{columns}";
        for (int i = 0; i < cards.Count; i++)
            lines[i + 1] = cards[i];

        return lines;
    }

    private static async Task Player(
        Board board,
        int playerNumber,
        int rows,
        int columns,
        int tries,
        int startDelay,
        bool noDelay,
        bool infinite)
    {
        var playerId = $"player{playerNumber}";

        if (startDelay > 0)
            await Task.Delay(startDelay);

        Console.WriteLine($"[{playerId}] Starting simulation {(infinite ? "(infinite)" : $"with {tries} attempts")}");

        int attempt = 0;
        while (infinite || attempt < tries)
        {
            attempt++;
            try
            {
                // thinking time
                var think1 = noDelay ? 1 : Random.Shared.Next(10, 100);
                if (think1 > 0) await Task.Delay(think1);

                var firstRow = RandomInt(rows);
                var firstCol = RandomInt(columns);

                Console.WriteLine($"[{playerId}] Attempt {attempt}: Flipping first card at ({firstRow}, {firstCol})");
                var afterFirst = await Commands.Flip(board, playerId, firstRow, firstCol);

                // between cards
                var think2 = noDelay ? 1: Random.Shared.Next(10, 100);
                if (think2 > 0) await Task.Delay(think2);

                var secondRow = RandomInt(rows);
                var secondCol = RandomInt(columns);

                Console.WriteLine($"[{playerId}] Attempt {attempt}: Flipping second card at ({secondRow}, {secondCol})");
                var afterSecond = await Commands.Flip(board, playerId, secondRow, secondCol);

                // naive match check: count "my " on this player's view
                var lines = afterSecond.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var controlledCount = lines.Count(line => line.StartsWith("my "));
                if (controlledCount == 2)
                    Console.WriteLine($"[{playerId}] + MATCH!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{playerId}] Attempt {attempt} failed: {ex.Message}");
            }

            // if infinite, loop continues until you press Ctrl+C to kill the process
        }

        Console.WriteLine($"[{playerId}] Simulation {(infinite ? "stopped" : "complete")}");
    }

    private static int RandomInt(int max) => Random.Shared.Next(max);
}

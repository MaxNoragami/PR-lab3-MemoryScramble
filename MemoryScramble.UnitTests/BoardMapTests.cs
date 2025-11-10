using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

public class BoardMapTests
{
    private static async Task<Board> LoadBoard() =>
        await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
    
    private static string[] Lines(string s) =>
        s.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string SpotAt(string viewBy, int row, int col, int rows = 5, int cols = 5)
    {
        var lines = Lines(viewBy);
        Assert.StartsWith($"{rows}x{cols}", lines[0]);
        var idx = 1 + row * cols + col;
        return lines[idx];
    }

    [Fact]
    public async Task Given_TransformerAtoX_When_Map_Then_AsBecomeXAndBsStayB()
    {
        var board = await LoadBoard();

        // A -> X, B -> B (unchanged)
        await board.Map(async s => s == "A" ? "X" : s);

        // Flip two original A positions: (0,0) and (0,2).
        // After map, they must match on "X".
        var pid = "p1";
        await board.Flip(pid, 0, 0); // first card
        await board.Flip(pid, 0, 2); // second card, should match X/X

        var view = board.ViewBy(pid);
        Assert.Equal("my X", SpotAt(view, 0, 0));
        Assert.Equal("my X", SpotAt(view, 0, 2));
    }

    [Fact]
    public async Task Given_IdentityTransformer_When_Map_Then_NoVisibleChange()
    {
        var board = await LoadBoard();
        var pid = "p1";

        // Start a watch
        var watchTask = board.Watch(pid);

        // Identity transform: no visible string changes
        await board.Map(s => Task.FromResult(s));

        // Watch must still be pending after a short period (no change occurred)
        var completed = await Task.WhenAny(watchTask, Task.Delay(50));
        Assert.NotSame(watchTask, completed);
    }

    [Fact]
    public async Task Given_RemovedPair_When_Map_Then_RemovedStaysNone()
    {
        var board = await LoadBoard();
        var pid = "p1";

        // Make a matching A/A pair and then trigger cleanup to remove them.
        // A’s at (0,0) and (0,2)
        await board.Flip(pid, 0, 0); // first
        await board.Flip(pid, 0, 2); // second -> matched and controlled

        // Next move (any first card) triggers cleanup (3-A removal)
        // choose an unrelated B at (0,1)
        await board.Flip(pid, 0, 1);

        // Verify they are removed ("none")
        var preMap = board.ViewBy(pid);
        Assert.Equal("none", SpotAt(preMap, 0, 0));
        Assert.Equal("none", SpotAt(preMap, 0, 2));

        // Map A->X, B->Y
        await board.Map(s => Task.FromResult(s == "A" ? "X" : "Y"));

        // Removed spots must remain none
        var postMap = board.ViewBy(pid);
        Assert.Equal("none", SpotAt(postMap, 0, 0));
        Assert.Equal("none", SpotAt(postMap, 0, 2));
    }

    [Fact]
    public async Task Given_InvalidTransformerOutput_When_Map_Then_ThrowsAndBoardUnchanged()
    {
        var board = await LoadBoard();
        var pid = "p1";

        // Snapshot a couple of known positions to assert unchanged later
        await board.Flip(pid, 0, 1); // (0,1) is B -> up/controlled B
        var before = board.ViewBy(pid);
        Assert.Equal("my B", SpotAt(before, 0, 1));

        // f returns invalid whitespace to force failure
        async Task<string> Bad(string s) => await Task.FromResult("  ");

        await Assert.ThrowsAsync<ArgumentException>(() => board.Map(Bad));

        // Board state should be unchanged
        var after = board.ViewBy(pid);
        Assert.Equal("my B", SpotAt(after, 0, 1));
    }

    [Fact]
    public async Task Given_MapRunsConcurrentlyBetweenFlips_When_MatchingPair_Then_StillMatches()
    {
        // (0,0) and (0,2) are both 'A' in the starting board
        var board = await LoadBoard();
        var pid = "p1";

        // First flip: control (0,0) which is 'A'
        await board.Flip(pid, 0, 0);
        var v1 = board.ViewBy(pid);
        Assert.Equal("my A", SpotAt(v1, 0, 0));

        // Start map() concurrently, transforming A -> X, leaving B unchanged.
        // Use a gate so we know map has *started* processing A before we proceed.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<string> F(string s)
        {
            if (s == "A")
            {
                aInvoked.TrySetResult();
                // Wait on gate so caller can control when A→X actually applies.
                return gate.Task.ContinueWith(_ => "X");
            }
            return Task.FromResult(s); // leave others unchanged
        }

        var mapTask = Task.Run(() => board.Map(F));

        // Ensure the A-branch in map() has been invoked (map is in-flight on 'A')
        await aInvoked.Task;

        // Release the gate so A->X can be applied at some point soon
        gate.SetResult();

        // Without awaiting map(), immediately attempt the second flip on the other original 'A' (0,2).
        // Depending on timing, this second flip will see:
        //   - both still 'A' (map not applied yet), or
        //   - both 'X' (map just applied to the whole A-group).
        await board.Flip(pid, 0, 2);

        var v2 = board.ViewBy(pid);
        var s00 = SpotAt(v2, 0, 0);
        var s02 = SpotAt(v2, 0, 2);

        // They must match and be controlled by the player, with either value A or X.
        Assert.StartsWith("my ", s00);
        Assert.StartsWith("my ", s02);
        Assert.Equal(s00, s02);
        Assert.True(s00 is "my A" or "my X");

        // Let map finish before the next move
        await mapTask;

        // Next move triggers 3-A cleanup (removal of matched pair)
        await board.Flip(pid, 0, 1); // (0,1) is a B in the starting board
        var v3 = board.ViewBy(pid);

        Assert.Equal("none", SpotAt(v3, 0, 0));
        Assert.Equal("none", SpotAt(v3, 0, 2));
        Assert.Equal("my B", SpotAt(v3, 0, 1));
    }
}

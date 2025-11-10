using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

public class BoardWatchTests
{
    private static async Task<Board> LoadBoard()
        => await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");

    private static string SpotAt(string boardState, int row, int col)
    {
        var lines = boardState.Replace("\r", string.Empty)
                              .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines[1 + row * 5 + col];
    }

    [Fact]
    public async Task Given_FirstFlip_When_Watch_Then_ResolvesWithMyUp()
    {
        var board = await LoadBoard();
        var pid = "p1";

        var watchTask = board.Watch(pid);

        await board.Flip(pid, 0, 0); // A turns up & becomes "my"

        var completed = await Task.WhenAny(watchTask, Task.Delay(500));
        Assert.Same(watchTask, completed);

        var state = await watchTask;
        Assert.Equal("my A", SpotAt(state, 0, 0));
    }

    [Fact]
    public async Task Given_NextMoveTurnsDownNonMatches_When_Watch_Then_ResolvesOnTurnDown()
    {
        var board = await LoadBoard();
        var pid = "p1";

        // Make a non-matching pair face-up & uncontrolled
        await board.Flip(pid, 0, 0); // A
        await board.Flip(pid, 0, 1); // B -> relinquish both (face-up but uncontrolled)

        // Start a watch, next move should turn them down (3-B), which is a visible change
        var watchTask = board.Watch(pid);

        await board.Flip(pid, 0, 2); // new first move triggers 3-B turn-down before flipping

        var completed = await Task.WhenAny(watchTask, Task.Delay(500));
        Assert.Same(watchTask, completed);

        var state = await watchTask;
        Assert.Equal("down", SpotAt(state, 0, 0));
        Assert.Equal("down", SpotAt(state, 0, 1));
        Assert.Equal("my A", SpotAt(state, 0, 2));
    }

    [Fact]
    public async Task Given_MatchedPairCleanup_When_Watch_Then_ResolvesOnRemoval()
    {
        var board = await LoadBoard();
        var pid = "p1";

        // Make a matched pair (A at (0,0) & (0,2))
        await board.Flip(pid, 0, 0); // A
        await board.Flip(pid, 0, 2); // A -> matched, both controlled by pid

        // Cleanup/removal happens when player makes their next first-card move (3-A)
        var watchTask = board.Watch(pid);

        await board.Flip(pid, 0, 1); // Next first move triggers removal of the matched pair

        var completed = await Task.WhenAny(watchTask, Task.Delay(500));
        Assert.Same(watchTask, completed);

        var state = await watchTask;
        Assert.Equal("none", SpotAt(state, 0, 0));
        Assert.Equal("none", SpotAt(state, 0, 2));
        Assert.Equal("my B", SpotAt(state, 0, 1));
    }

    [Fact]
    public async Task Given_MapChangesStrings_When_Watch_Then_ResolvesEvenIfCardsAreDown()
    {
        var board = await LoadBoard();
        var pid = "p1";

        var watchTask = board.Watch(pid);

        // Change all A -> X; this is a string change and MUST trigger watch()
        await board.Map(s => Task.FromResult(s == "A" ? "X" : s));

        var completed = await Task.WhenAny(watchTask, Task.Delay(500));
        Assert.Same(watchTask, completed);

        // The view may still be "down" at most spots; we only assert that watch resolved.
        _ = await watchTask;
    }

    [Fact]
    public async Task Given_ControlOnly_NoFaceChange_When_Watch_Then_DoesNotResolve()
    {
        var board = await LoadBoard();
        var p0 = "p0";
        var p1 = "p1";

        // Create a face-up & uncontrolled card at (0,0):
        // p0 flips non-matching pair, relinquishing control but leaving them face up.
        await board.Flip(p0, 0, 0); // A
        await board.Flip(p0, 0, 1); // B -> both up & uncontrolled now

        // Start watch for p1
        var watchTask = board.Watch(p1);

        // p1 takes control of (0,0) per 1-C (already face-up & uncontrolled) => no visible change
        await board.Flip(p1, 0, 0);
        // p0 takes control of (0,1) per 1-C (already face-up & uncontrolled) => no visible change
        await board.Flip(p0, 0, 1);

        // Watch must still be pending (no face-up/down/removal/string change)
        var completed = await Task.WhenAny(watchTask, Task.Delay(150));
        Assert.NotSame(watchTask, completed);
    }

    [Fact]
    public async Task Given_MultipleWatchers_When_ChangeOccurs_Then_EachGetsPersonalizedView()
    {
        var board = await LoadBoard();
        var p1 = "alice";
        var p2 = "bob";

        var w1 = board.Watch(p1);
        var w2 = board.Watch(p2);

        // alice flips (0,0) to trigger the change
        await board.Flip(p1, 0, 0);

        var c1 = await Task.WhenAny(w1, Task.Delay(500));
        var c2 = await Task.WhenAny(w2, Task.Delay(500));
        Assert.Same(w1, c1);
        Assert.Same(w2, c2);

        var s1 = await w1;
        var s2 = await w2;

        Assert.Equal("my A", SpotAt(s1, 0, 0)); // personalized for alice
        Assert.Equal("up A", SpotAt(s2, 0, 0)); // seen as "up" by bob
    }

    [Fact]
    public async Task Given_IdentityTransformer_When_Map_Then_NoVisibleChange()
    {
        var board = await LoadBoard();
        var pid = "p1";

        var watchTask = board.Watch(pid);

        await board.Map(s => Task.FromResult(s)); // identity

        var completed = await Task.WhenAny(watchTask, Task.Delay(150));
        Assert.NotSame(watchTask, completed);
    }
}

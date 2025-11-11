using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

/// <summary>
/// Tests for Board.Watch() functionality - verifying watcher notifications on board changes.
/// </summary>
public class BoardWatchTests
{
    /// <summary>
    /// Helper method to load a standard 5x5 test board.
    /// </summary>
    private static async Task<Board> LoadBoard()
        => await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");

    /// <summary>
    /// Helper method to get the card state at a specific board position.
    /// </summary>
    private static string SpotAt(string boardState, int row, int col)
    {
        var lines = boardState.Replace("\r", string.Empty)
                              .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines[1 + row * 5 + col];
    }

    /// <summary>
    /// Tests that Watch() resolves when a card is flipped (face-up change).
    /// </summary>
    [Fact]
    public async Task Given_FirstFlip_When_Watch_Then_ResolvesWithMyUp()
    {
        // Arrange: Load board and start watching
        var board = await LoadBoard();
        var pid = "p1";

        var watchTask = board.Watch(pid);

        // Act: Flip a card (triggers visible change)
        await board.Flip(pid, 0, 0); // A turns up & becomes "my"

        // Assert: Watch should complete within timeout
        var completed = await Task.WhenAny(watchTask, Task.Delay(500));
        Assert.Same(watchTask, completed);

        var state = await watchTask;
        Assert.Equal("my A", SpotAt(state, 0, 0));
    }

    /// <summary>
    /// Tests that Watch() resolves when non-matched cards are turned face-down (Rule 3-B).
    /// </summary>
    [Fact]
    public async Task Given_NextMoveTurnsDownNonMatches_When_Watch_Then_ResolvesOnTurnDown()
    {
        // Arrange: Create non-matching pair (face-up but uncontrolled)
        var board = await LoadBoard();
        var pid = "p1";

        await board.Flip(pid, 0, 0); // A
        await board.Flip(pid, 0, 1); // B -> relinquish both (face-up but uncontrolled)

        // Start watching before triggering cleanup
        var watchTask = board.Watch(pid);

        // Act: New first move triggers Rule 3-B (turn down previous non-matched cards)
        await board.Flip(pid, 0, 2); // new first move triggers 3-B turn-down before flipping

        // Assert: Watch resolves due to cards turning face-down
        var completed = await Task.WhenAny(watchTask, Task.Delay(500));
        Assert.Same(watchTask, completed);

        var state = await watchTask;
        Assert.Equal("down", SpotAt(state, 0, 0)); // Turned down
        Assert.Equal("down", SpotAt(state, 0, 1)); // Turned down
        Assert.Equal("my A", SpotAt(state, 0, 2));  // New controlled card
    }

    /// <summary>
    /// Tests that Watch() resolves when matched cards are removed (Rule 3-A).
    /// </summary>
    [Fact]
    public async Task Given_MatchedPairCleanup_When_Watch_Then_ResolvesOnRemoval()
    {
        // Arrange: Create a matched pair
        var board = await LoadBoard();
        var pid = "p1";

        await board.Flip(pid, 0, 0); // A
        await board.Flip(pid, 0, 2); // A -> matched, both controlled by pid

        // Start watching before triggering removal
        var watchTask = board.Watch(pid);

        // Act: Cleanup/removal happens on next first-card move (Rule 3-A)
        await board.Flip(pid, 0, 1); // Next first move triggers removal of the matched pair

        // Assert: Watch resolves due to card removal
        var completed = await Task.WhenAny(watchTask, Task.Delay(500));
        Assert.Same(watchTask, completed);

        var state = await watchTask;
        Assert.Equal("none", SpotAt(state, 0, 0)); // Removed
        Assert.Equal("none", SpotAt(state, 0, 2)); // Removed
        Assert.Equal("my B", SpotAt(state, 0, 1));  // New controlled card
    }

    /// <summary>
    /// Tests that Watch() resolves when Map() changes card strings.
    /// </summary>
    [Fact]
    public async Task Given_MapChangesStrings_When_Watch_Then_ResolvesEvenIfCardsAreDown()
    {
        // Arrange: Load board and start watching
        var board = await LoadBoard();
        var pid = "p1";

        var watchTask = board.Watch(pid);

        // Act: Transform card strings (A -> X), which is a visible change
        await board.Map(s => Task.FromResult(s == "A" ? "X" : s));

        // Assert: Watch must resolve on string change
        var completed = await Task.WhenAny(watchTask, Task.Delay(500));
        Assert.Same(watchTask, completed);

        // The view may still be "down" at most spots; we only assert that watch resolved.
        _ = await watchTask;
    }

    /// <summary>
    /// Tests that Watch() does NOT resolve on control changes without visible state changes (Rule 1-C).
    /// </summary>
    [Fact]
    public async Task Given_ControlOnly_NoFaceChange_When_Watch_Then_DoesNotResolve()
    {
        // Arrange: Create face-up & uncontrolled cards
        var board = await LoadBoard();
        var p0 = "p0";
        var p1 = "p1";

        // p0 flips non-matching pair, relinquishing control but leaving them face up.
        await board.Flip(p0, 0, 0); // A
        await board.Flip(p0, 0, 1); // B -> both up & uncontrolled now

        // Start watch for p1
        var watchTask = board.Watch(p1);

        // Act: Players take control of already face-up cards (Rule 1-C: no visible change)
        await board.Flip(p1, 0, 0); // p1 takes control (no face change)
        await board.Flip(p0, 0, 1); // p0 takes control (no face change)

        // Assert: Watch must still be pending (no face-up/down/removal/string change)
        var completed = await Task.WhenAny(watchTask, Task.Delay(150));
        Assert.NotSame(watchTask, completed);
    }

    /// <summary>
    /// Tests that multiple watchers each receive personalized views.
    /// </summary>
    [Fact]
    public async Task Given_MultipleWatchers_When_ChangeOccurs_Then_EachGetsPersonalizedView()
    {
        // Arrange: Two players watching
        var board = await LoadBoard();
        var p1 = "alice";
        var p2 = "bob";

        var w1 = board.Watch(p1);
        var w2 = board.Watch(p2);

        // Act: alice flips (0,0) to trigger the change
        await board.Flip(p1, 0, 0);

        // Assert: Both watchers resolve
        var c1 = await Task.WhenAny(w1, Task.Delay(500));
        var c2 = await Task.WhenAny(w2, Task.Delay(500));
        Assert.Same(w1, c1);
        Assert.Same(w2, c2);

        var s1 = await w1;
        var s2 = await w2;

        // alice sees "my A" (she controls it), bob sees "up A"
        Assert.Equal("my A", SpotAt(s1, 0, 0)); // personalized for alice
        Assert.Equal("up A", SpotAt(s2, 0, 0)); // seen as "up" by bob
    }

    /// <summary>
    /// Tests that Watch() does NOT resolve when Map() applies identity transformation (no visible change).
    /// </summary>
    [Fact]
    public async Task Given_IdentityTransformer_When_Map_Then_NoVisibleChange()
    {
        // Arrange: Load board and start watching
        var board = await LoadBoard();
        var pid = "p1";

        var watchTask = board.Watch(pid);

        // Act: Apply identity transform (no actual string changes)
        await board.Map(s => Task.FromResult(s)); // identity

        // Assert: Watch should NOT resolve (no visible change)
        var completed = await Task.WhenAny(watchTask, Task.Delay(150));
        Assert.NotSame(watchTask, completed);
    }
}

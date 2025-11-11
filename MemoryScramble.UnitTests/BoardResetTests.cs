using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

/// <summary>
/// Tests for Board.Reset() functionality.
/// </summary>
public class BoardResetTests
{
    private static string[] GetLines(string boardState)
    {
        return boardState.Replace("\r", string.Empty)
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string SpotAt(string boardState, int row, int col, int columns)
    {
        var lines = GetLines(boardState);
        return lines[1 + row * columns + col];
    }

    /// <summary>
    /// Tests that Reset restores all cards to their initial state face-down.
    /// </summary>
    [Fact]
    public async Task Given_ModifiedBoard_When_Reset_Then_RestoresAllCardsToInitialState()
    {
        // Arrange: Create a 2x2 board
        var lines = new[] { "2x2", "A", "B", "A", "B" };
        var board = Board.ParseFromLines(lines);

        // Flip some cards
        await board.Flip("player1", 0, 0); // First card (A)
        await board.Flip("player1", 1, 1); // Second card (B) - no match
        await board.Flip("player1", 0, 1); // New first card (B)

        // Act: Reset the board
        await board.Reset();

        // Assert: All cards should be face-down and in original positions
        var view = await board.ViewBy("player1");
        var linesAfter = GetLines(view);
        
        Assert.Equal("2x2", linesAfter[0]);
        Assert.Equal("down", linesAfter[1]); // (0,0) - A, face down
        Assert.Equal("down", linesAfter[2]); // (0,1) - B, face down
        Assert.Equal("down", linesAfter[3]); // (1,0) - A, face down
        Assert.Equal("down", linesAfter[4]); // (1,1) - B, face down
    }

    /// <summary>
    /// Tests that Reset clears all player control.
    /// </summary>
    [Fact]
    public async Task Given_PlayerControlsCards_When_Reset_Then_ClearsPlayerControl()
    {
        // Arrange
        var lines = new[] { "2x2", "A", "A", "B", "B" };
        var board = Board.ParseFromLines(lines);

        // Player flips first card and controls it
        await board.Flip("player1", 0, 0);
        var viewBefore = await board.ViewBy("player1");
        Assert.Contains("my A", viewBefore); // Player controls the card

        // Act: Reset
        await board.Reset();

        // Assert: Player should not control any cards
        var viewAfter = await board.ViewBy("player1");
        Assert.DoesNotContain("my ", viewAfter);
        Assert.Contains("down", viewAfter);
    }

    /// <summary>
    /// Tests that Reset clears player states (first and second card tracking).
    /// </summary>
    [Fact]
    public async Task Given_PlayerInMidGame_When_Reset_Then_ClearsPlayerStates()
    {
        // Arrange
        var lines = new[] { "3x3", "A", "B", "C", "A", "B", "C", "D", "D", "E" };
        var board = Board.ParseFromLines(lines);

        // Player makes multiple moves
        await board.Flip("player1", 0, 0); // First card
        await board.Flip("player1", 1, 0); // Second card (match)
        
        // Act: Reset
        await board.Reset();

        // Assert: Player can flip cards normally again (no previous state)
        await board.Flip("player1", 0, 0); // Should work as first card
        var view = await board.ViewBy("player1");
        Assert.Contains("my A", view); // Player controls first card
    }

    /// <summary>
    /// Tests that Reset works with multiple players.
    /// </summary>
    [Fact]
    public async Task Given_MultiplePlayers_When_Reset_Then_ClearsAllPlayerStates()
    {
        // Arrange
        var lines = new[] { "2x2", "X", "Y", "X", "Y" };
        var board = Board.ParseFromLines(lines);

        // Multiple players make moves
        await board.Flip("player1", 0, 0);
        await board.Flip("player2", 0, 1);

        // Act: Reset
        await board.Reset();

        // Assert: Both players see reset board
        var view1 = await board.ViewBy("player1");
        var view2 = await board.ViewBy("player2");

        Assert.DoesNotContain("my ", view1);
        Assert.DoesNotContain("my ", view2);
        Assert.Equal(4, view1.Split("down").Length - 1); // 4 down cards
        Assert.Equal(4, view2.Split("down").Length - 1); // 4 down cards
    }

    /// <summary>
    /// Tests that Reset restores cards that were removed (matched).
    /// </summary>
    [Fact]
    public async Task Given_MatchedCards_When_Reset_Then_RestoresRemovedCards()
    {
        // Arrange: Create board and match some cards
        var lines = new[] { "2x2", "A", "A", "B", "B" };
        var board = Board.ParseFromLines(lines);

        // Match and remove first pair
        await board.Flip("player1", 0, 0); // First A
        await board.Flip("player1", 0, 1); // Second A (match!)
        await board.Flip("player1", 1, 0); // Triggers cleanup, removes matched cards

        var viewBefore = await board.ViewBy("player1");
        Assert.Contains("none", viewBefore); // Cards removed

        // Act: Reset
        await board.Reset();

        // Assert: Removed cards are restored
        var viewAfter = await board.ViewBy("player1");
        Assert.DoesNotContain("none", viewAfter); // No empty spaces
        Assert.Equal(4, viewAfter.Split("down").Length - 1); // All 4 cards present
    }

    /// <summary>
    /// Tests that Reset notifies watchers of the board change.
    /// </summary>
    [Fact]
    public async Task Given_ActiveWatcher_When_Reset_Then_NotifiesWatcher()
    {
        // Arrange
        var lines = new[] { "2x2", "A", "B", "A", "B" };
        var board = Board.ParseFromLines(lines);

        // Start watching
        var watchTask = board.Watch("player1");

        // Give watcher time to register
        await Task.Delay(10);

        // Act: Reset triggers watchers
        await board.Reset();

        // Assert: Watcher should complete
        var watchResult = await Task.WhenAny(watchTask, Task.Delay(1000));
        Assert.Same(watchTask, watchResult); // Watcher should be notified

        var watchView = await watchTask;
        Assert.Contains("2x2", watchView);
        Assert.Contains("down", watchView);
    }

    /// <summary>
    /// Tests that Reset rejects waiting operations.
    /// </summary>
    [Fact]
    public async Task Given_WaitingOperation_When_Reset_Then_RejectsWaitingOperation()
    {
        // Arrange
        var lines = new[] { "2x2", "A", "B", "C", "D" };
        var board = Board.ParseFromLines(lines);

        // Player1 controls a card
        await board.Flip("player1", 0, 0);

        // Player2 tries to flip the same card (will wait)
        var waitingTask = board.Flip("player2", 0, 0);

        // Give it time to start waiting
        await Task.Delay(50);

        // Act: Reset while player2 is waiting
        await board.Reset();

        // Assert: Waiting operation should be canceled
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await waitingTask;
        });
    }

    /// <summary>
    /// Tests that Reset can be called multiple times.
    /// </summary>
    [Fact]
    public async Task Given_RepeatedResets_When_Reset_Then_CanBeCalledMultipleTimes()
    {
        // Arrange
        var lines = new[] { "2x2", "A", "B", "A", "B" };
        var board = Board.ParseFromLines(lines);

        // Act & Assert: Reset multiple times
        await board.Reset(); // First reset
        var view1 = await board.ViewBy("player1");
        Assert.Equal(4, view1.Split("down").Length - 1);

        // Make some changes
        await board.Flip("player1", 0, 0);

        await board.Reset(); // Second reset
        var view2 = await board.ViewBy("player1");
        Assert.Equal(4, view2.Split("down").Length - 1);

        await board.Reset(); // Third reset
        var view3 = await board.ViewBy("player1");
        Assert.Equal(4, view3.Split("down").Length - 1);
    }

    /// <summary>
    /// Tests that Reset preserves the board dimensions and structure.
    /// </summary>
    [Fact]
    public async Task Given_DifferentBoardSize_When_Reset_Then_PreservesBoardDimensions()
    {
        // Arrange
        var lines = new[] { "3x4", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L" };
        var board = Board.ParseFromLines(lines);

        var originalRows = board.Rows;
        var originalColumns = board.Columns;

        // Act: Reset
        await board.Reset();

        // Assert: Dimensions unchanged
        Assert.Equal(originalRows, board.Rows);
        Assert.Equal(originalColumns, board.Columns);

        var view = await board.ViewBy("player1");
        Assert.StartsWith("3x4", view);
    }

    /// <summary>
    /// Tests that operations work normally after Reset.
    /// </summary>
    [Fact]
    public async Task Given_ResetBoard_When_OperationsPerformed_Then_AllowsNormalOperations()
    {
        // Arrange
        var lines = new[] { "2x2", "A", "A", "B", "B" };
        var board = Board.ParseFromLines(lines);

        // Make some moves
        await board.Flip("player1", 0, 0);
        await board.Flip("player1", 0, 1);

        // Act: Reset
        await board.Reset();

        // Assert: Can play normally
        await board.Flip("player1", 0, 0); // First card
        var view1 = await board.ViewBy("player1");
        Assert.Contains("my A", view1);

        await board.Flip("player1", 0, 1); // Second card (match!)
        var view2 = await board.ViewBy("player1");
        Assert.Contains("my A", view2); // Both cards controlled (matched)
        // Trigger cleanup by flipping new card
        await board.Flip("player1", 1, 0);
        var view3 = await board.ViewBy("player1");
        Assert.Contains("none", view3); // Matched cards removed
    }

    /// <summary>
    /// Tests that Reset works correctly on a board with no moves made yet.
    /// </summary>
    [Fact]
    public async Task Given_UntouchedBoard_When_Reset_Then_WorksCorrectly()
    {
        // Arrange
        var lines = new[] { "2x2", "A", "B", "C", "D" };
        var board = Board.ParseFromLines(lines);

        // Act: Reset without making any moves
        await board.Reset();

        // Assert: Board state unchanged (all face-down)
        var view = await board.ViewBy("player1");
        var viewLines = GetLines(view);

        Assert.Equal("2x2", viewLines[0]);
        Assert.Equal("down", viewLines[1]);
        Assert.Equal("down", viewLines[2]);
        Assert.Equal("down", viewLines[3]);
        Assert.Equal("down", viewLines[4]);
    }

    /// <summary>
    /// Tests that Reset clears holds (waiting operations) properly.
    /// </summary>
    [Fact]
    public async Task Given_MultipleWaitingOperations_When_Reset_Then_ClearsAllHolds()
    {
        // Arrange
        var lines = new[] { "2x2", "A", "B", "C", "D" };
        var board = Board.ParseFromLines(lines);

        // Player1 controls a card
        await board.Flip("player1", 0, 0);

        // Multiple players try to flip the same card (all waiting)
        var wait1 = board.Flip("player2", 0, 0);
        var wait2 = board.Flip("player3", 0, 0);
        var wait3 = board.Flip("player4", 0, 0);

        await Task.Delay(50); // Let them start waiting

        // Act: Reset
        await board.Reset();

        // Assert: All waiting operations are canceled
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await wait1);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await wait2);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await wait3);
    }

    /// <summary>
    /// Tests Reset with Map operations (cards that have been transformed).
    /// </summary>
    [Fact]
    public async Task Given_MappedCards_When_Reset_Then_RestoresOriginalCards()
    {
        // Arrange
        var lines = new[] { "2x2", "A", "A", "B", "B" };
        var board = Board.ParseFromLines(lines);

        // Transform cards using Map
        await board.Map(async card => card == "A" ? "X" : "Y");

        var viewAfterMap = await board.ViewBy("player1");
        Assert.DoesNotContain("A", viewAfterMap); // A's are now X's

        // Act: Reset
        await board.Reset();

        // Assert: Original cards restored
        await board.Flip("player1", 0, 0);
        var viewAfterReset = await board.ViewBy("player1");
        Assert.Contains("my A", viewAfterReset); // Original A is back
    }
}

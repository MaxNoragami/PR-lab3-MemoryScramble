using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

/// <summary>
/// Tests for Board.ViewBy() functionality - verifying player-specific views of the board state.
/// </summary>
public class BoardViewByTests
{
    /// <summary>
    /// Helper method to load a standard 5x5 test board.
    /// </summary>
    private static async Task<Board> LoadBoard()
        => await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
    
    /// <summary>
    /// Helper method to split board state into lines, handling cross-platform line endings.
    /// </summary>
    private static string[] GetLines(string boardState) =>
        boardState.Replace("\r", string.Empty)
                  .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Helper method to get the card state at a specific board position.
    /// </summary>
    private static string SpotAt(string boardState, int row, int col)
    {
        var lines = GetLines(boardState);
        return lines[1 + row * 5 + col];
    }

    /// <summary>
    /// Tests that a freshly loaded board shows all cards face-down.
    /// </summary>
    [Fact]
    public async Task Given_InitialBoard_When_ViewBy_Then_ReturnsAllCardsDown()
    {
        // Arrange: Load a fresh 5x5 board
        var board = await LoadBoard();
        var playerId = "max123";

        // Act: Get the board view
        var boardState = await board.ViewBy(playerId);

        var lines = GetLines(boardState);

        // Assert: First line should be board dimensions, all cards should be face-down
        Assert.Equal("5x5", lines[0]);
        Assert.Equal(26, lines.Length); // 1 dimension line + 25 cards

        for (int i = 1; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    /// <summary>
    /// Tests that a player sees "my" prefix for cards they control.
    /// </summary>
    [Fact]
    public async Task Given_PlayerControlsCard_When_ViewBy_Then_ShowsMyCard()
    {
        // Arrange: Load board and flip a card
        var board = await LoadBoard();
        var playerId = "max123";

        // Act: Player flips their first card at (0,0)
        await board.Flip(playerId, 0, 0);
        var boardState = await board.ViewBy(playerId);

        var lines = GetLines(boardState);

        // Assert: Player should see "my A" for the card they control
        Assert.Equal("5x5", lines[0]);
        Assert.Equal("my A", SpotAt(boardState, 0, 0));

        // All other cards should still be face-down
        for (int i = 2; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    /// <summary>
    /// Tests that a player sees "up" prefix for cards controlled by other players.
    /// </summary>
    [Fact]
    public async Task Given_AnotherPlayerControlsCard_When_ViewBy_Then_ShowsUpCard()
    {
        // Arrange: Two players, first player controls a card
        var board = await LoadBoard();
        var player1 = "max123";
        var player2 = "johnPork";

        // Act: Player1 flips card, Player2 views the board
        await board.Flip(player1, 0, 0);
        var boardState = await board.ViewBy(player2);

        var lines = GetLines(boardState);

        // Assert: Player2 should see "up A" (not "my A") for player1's card
        Assert.Equal("5x5", lines[0]);
        Assert.Equal("up A", SpotAt(boardState, 0, 0));

        // All other cards should still be face-down
        for (int i = 2; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    /// <summary>
    /// Tests that matched and removed cards show as "none".
    /// </summary>
    [Fact]
    public async Task Given_MatchedPairRemoved_When_ViewBy_Then_ShowsNoneForRemovedCards()
    {
        // Arrange: Load board
        var board = await LoadBoard();
        var playerId = "max123";

        // Act: Make a matching pair (A at 0,0 and 0,2) and trigger cleanup
        await board.Flip(playerId, 0, 0); // First A
        await board.Flip(playerId, 0, 2); // Second A (matched)
        await board.Flip(playerId, 0, 1); // New first card triggers cleanup (Rule 3-A)

        var boardState = await board.ViewBy(playerId);

        var lines = GetLines(boardState);

        // Assert: Matched cards should be "none", new card should be controlled
        Assert.Equal("5x5", lines[0]);
        Assert.Equal("none", SpotAt(boardState, 0, 0)); // Removed
        Assert.Equal("my B", SpotAt(boardState, 0, 1));  // Currently controlled
        Assert.Equal("none", SpotAt(boardState, 0, 2)); // Removed

        // Rest should be face-down
        for (int i = 4; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    /// <summary>
    /// Tests a complex board state with multiple card states.
    /// </summary>
    [Fact]
    public async Task Given_ComplexBoardState_When_ViewBy_Then_ShowsCorrectMixedStates()
    {
        // Arrange: Load board and create complex state
        var board = await LoadBoard();
        var player1 = "max123";
        var player2 = "johnPork";

        // Act: Create complex board state with removed cards and controlled cards
        await board.Flip(player1, 0, 0); // A
        await board.Flip(player1, 0, 2); // A (matched)
        await board.Flip(player1, 0, 1); // B (triggers cleanup, removes matched A's)
        await board.Flip(player1, 0, 3); // B (matches with 0,1)

        var player1State = await board.ViewBy(player1);
        var player2State = await board.ViewBy(player2);

        // Assert: Player1 sees their controlled cards as "my"
        Assert.Equal("none", SpotAt(player1State, 0, 0)); // Removed
        Assert.Equal("my B", SpotAt(player1State, 0, 1));  // Controlled & matched
        Assert.Equal("none", SpotAt(player1State, 0, 2)); // Removed
        Assert.Equal("my B", SpotAt(player1State, 0, 3));  // Controlled & matched

        // Player2 sees player1's cards as "up"
        Assert.Equal("none", SpotAt(player2State, 0, 0)); // Removed
        Assert.Equal("up B", SpotAt(player2State, 0, 1));  // Visible but not controlled
        Assert.Equal("none", SpotAt(player2State, 0, 2)); // Removed
        Assert.Equal("up B", SpotAt(player2State, 0, 3));  // Visible but not controlled
    }

    /// <summary>
    /// Tests that non-matching cards remain face-up after second flip (Rule 2-E).
    /// </summary>
    [Fact]
    public async Task Given_NonMatchingCardsAfterSecondFlip_When_ViewBy_Then_ShowsBothCardsUp()
    {
        // Arrange: Load board
        var board = await LoadBoard();
        var player1 = "max123";
        var player2 = "johnPork";

        // Act: Flip non-matching cards (Rule 2-E: control relinquished but cards stay up)
        await board.Flip(player1, 0, 0); // A
        await board.Flip(player1, 0, 1); // B (no match)

        var player1State = await board.ViewBy(player1);
        var player2State = await board.ViewBy(player2);

        // Assert: Both players see both cards as face-up but uncontrolled
        Assert.Equal("up A", SpotAt(player1State, 0, 0));
        Assert.Equal("up B", SpotAt(player1State, 0, 1));

        Assert.Equal("up A", SpotAt(player2State, 0, 0));
        Assert.Equal("up B", SpotAt(player2State, 0, 1));
    }

    /// <summary>
    /// Tests that ViewBy throws ArgumentException for invalid player IDs.
    /// </summary>
    [Fact]
    public async Task Given_EmptyPlayerId_When_ViewBy_Then_ThrowsArgumentException()
    {
        // Arrange: Load board
        var board = await LoadBoard();

        // Act & Assert: Invalid player IDs should throw
        await Assert.ThrowsAsync<ArgumentException>(() => board.ViewBy(""));
        await Assert.ThrowsAsync<ArgumentException>(() => board.ViewBy("   "));
        await Assert.ThrowsAsync<ArgumentException>(() => board.ViewBy(null!));
    }

    /// <summary>
    /// Tests that multiple players each see their personalized view of the board.
    /// </summary>
    [Fact]
    public async Task Given_MultiplePlayersWithDifferentStates_When_ViewBy_Then_EachPlayerSeesCorrectState()
    {
        // Arrange: Load board with three players
        var board = await LoadBoard();
        var player1 = "alice";
        var player2 = "bob";
        var player3 = "charlie";

        // Act: Create overlapping non-matched pairs
        await board.Flip(player1, 0, 0); // A
        await board.Flip(player1, 1, 0); // B (no match)

        await board.Flip(player2, 0, 2); // A
        await board.Flip(player2, 1, 2); // B (no match)

        await board.Flip(player3, 0, 1); // B (first card)

        var aliceState = await board.ViewBy(player1);
        var bobState = await board.ViewBy(player2);
        var charlieState = await board.ViewBy(player3);

        // Assert: All players see the same face-up cards (all uncontrolled except charlie's)
        Assert.Equal("up A", SpotAt(aliceState, 0, 0));
        Assert.Equal("up B", SpotAt(aliceState, 0, 1));
        Assert.Equal("up A", SpotAt(aliceState, 0, 2));
        Assert.Equal("up B", SpotAt(aliceState, 1, 0));
        Assert.Equal("up B", SpotAt(aliceState, 1, 2));

        Assert.Equal("up A", SpotAt(bobState, 0, 0));
        Assert.Equal("up B", SpotAt(bobState, 0, 1));
        Assert.Equal("up A", SpotAt(bobState, 0, 2));
        Assert.Equal("up B", SpotAt(bobState, 1, 0));
        Assert.Equal("up B", SpotAt(bobState, 1, 2));

        // Charlie controls one card, sees others as "up"
        Assert.Equal("up A", SpotAt(charlieState, 0, 0));
        Assert.Equal("my B", SpotAt(charlieState, 0, 1)); // Only difference
        Assert.Equal("up A", SpotAt(charlieState, 0, 2));
        Assert.Equal("up B", SpotAt(charlieState, 1, 0));
        Assert.Equal("up B", SpotAt(charlieState, 1, 2));
    }

    /// <summary>
    /// Tests that cleanup (Rule 3-B) turns non-matched cards face-down.
    /// </summary>
    [Fact]
    public async Task Given_BoardStateAfterCleanup_When_ViewBy_Then_ShowsCorrectTurnedDownCards()
    {
        // Arrange: Load board
        var board = await LoadBoard();
        var playerId = "max123";

        // Act: Create non-matching pair, then flip new card to trigger cleanup
        await board.Flip(playerId, 0, 0); // A
        await board.Flip(playerId, 0, 1); // B (no match, both stay up)
        await board.Flip(playerId, 0, 4); // A (Rule 3-B: turns previous cards down)

        var boardState = await board.ViewBy(playerId);

        var lines = GetLines(boardState);

        // Assert: Previous cards should be face-down, new card controlled
        Assert.Equal("5x5", lines[0]);
        Assert.Equal("down", SpotAt(boardState, 0, 0)); // Turned back down
        Assert.Equal("down", SpotAt(boardState, 0, 1)); // Turned back down
        Assert.Equal("my A", SpotAt(boardState, 0, 4));  // Currently controlled

        // Verify other cards remain down
        for (int i = 3; i <= 4; i++)
            Assert.Equal("down", lines[i]);
        for (int i = 6; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }
}

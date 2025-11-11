using MemoryScramble.API.Exceptions;
using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

/// <summary>
/// Tests for Board.Flip() functionality - verifying the game rules for flipping cards.
/// </summary>
public class BoardFlipTests
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
    /// Tests Rule 1-A: When trying to flip an empty space as the first card,
    /// throws NoCardAtPositionException.
    /// </summary>
    [Fact]
    public async Task Rule1A_Given_EmptySpace_When_FlipFirstCard_Then_ThrowsFlipException()
    {
        // Arrange: Load board and create matched pair to leave empty spaces
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)
        await board.Flip(firstPlayerId, 0, 1); // B - FirstCard P1 (triggers cleanup of matched pair)

        // Act & Assert: Rule 1-A - Cannot flip empty space as first card
        await Assert.ThrowsAsync<NoCardAtPositionException>(() =>
            board.Flip(secondPlayerId, 0, 0)); // P2 try select as FirstCard an empty space

        // Assert: Verify the empty spaces remain
        var boardState = await board.ViewBy(secondPlayerId);
        Assert.Equal("none", SpotAt(boardState, 0, 0)); // empty (removed matched card)
        Assert.Equal("none", SpotAt(boardState, 0, 2)); // empty (removed matched card)
    }

    /// <summary>
    /// Tests Rule 1-B: When flipping a face-down card as the first card,
    /// the card turns face up for all players and the flipping player gains control.
    /// </summary>
    [Fact]
    public async Task Rule1B_Given_SelectFacingDownCard_When_FlipFirstCard_Then_CardControlAndFacesUpForEveryone()
    {
        // Arrange: Load board with two players
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        // Act: Rule 1-B - Flip face-down card as first card
        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1

        // Assert: Card is face up for everyone, controlled by first player
        var firstBoardState = await board.ViewBy(firstPlayerId);
        var secondBoardState = await board.ViewBy(secondPlayerId);

        Assert.Equal("my A", SpotAt(firstBoardState, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(secondBoardState, 0, 0)); // second views card up - A
    }

    /// <summary>
    /// Tests Rule 1-C: When flipping a face-up uncontrolled card as the first card,
    /// the player gains control and the card remains face up for all players.
    /// </summary>
    [Fact]
    public async Task Rule1C_Given_SelectFacingDownCard_When_FlipFirstCard_Then_CardControlAndRemainsFaceUpForEveryone()
    {
        // Arrange: Create face-up uncontrolled card (by making non-matching pair first)
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match, both release control)
        var boardStateBeforeFirst = await board.ViewBy(firstPlayerId);
        var boardStateBeforeSecond = await board.ViewBy(secondPlayerId);

        // Act: Rule 1-C - Second player flips face-up uncontrolled card
        await board.Flip(secondPlayerId, 0, 1); // B - FirstCard P2

        // Assert: Second player gains control, card remains face up
        var firstBoardStateAfter = await board.ViewBy(firstPlayerId);
        var secondBoardStateAfter = await board.ViewBy(secondPlayerId);

        Assert.Equal("up B", SpotAt(boardStateBeforeFirst, 0, 1)); // first views card up - B (uncontrolled)
        Assert.Equal("up B", SpotAt(boardStateBeforeSecond, 0, 1)); // second views card up - B (uncontrolled)
        Assert.Equal("up B", SpotAt(firstBoardStateAfter, 0, 1)); // first views card up - B
        Assert.Equal("my B", SpotAt(secondBoardStateAfter, 0, 1)); // second controls first card - B
    }

    /// <summary>
    /// Tests Rule 1-D: When a player tries to flip a card controlled by another player as their first card,
    /// the operation waits until the card becomes available, then completes successfully.
    /// </summary>
    [Fact]
    public async Task Rule1D_Given_SelectAlreadyControlledCard_When_FlipFirstCard_Then_WaitsUntilAvailable()
    {
        // Arrange: Load board with two players
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        // Rule 1-D: Card is controlled by another player, so second player's flip waits
        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1

        // Act: Player2 tries to flip the same card (will wait/hold)
        var p2FlipTask = board.Flip(secondPlayerId, 0, 0); // A - FirstCard P2 (on holding)

        await Task.Delay(50);
        Assert.False(p2FlipTask.IsCompleted); // Should still be waiting

        // Player1 makes a non-matching second flip, releasing control of first card
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)

        // Assert: Now Player2's waiting flip should complete
        await p2FlipTask;
        var boardStateSecond = await board.ViewBy(secondPlayerId);
        var boardStateFirst = await board.ViewBy(firstPlayerId);

        Assert.Equal("up A", SpotAt(boardStateFirst, 0, 0)); // first views card up - A (no longer controlled)
        Assert.Equal("my A", SpotAt(boardStateSecond, 0, 0)); // second controls first card - A (acquired after waiting)
    }

    /// <summary>
    /// Tests Rule 2-A: When trying to flip an empty space as the second card,
    /// throws NoCardAtPositionException and player loses control of their first card.
    /// </summary>
    [Fact]
    public async Task Rule2A_Given_EmptySpace_When_FlipSecondCard_Then_ThrowsFlipExceptionAndGiveUpControlFirstCard()
    {
        var board = await LoadBoard();
        var playerId = "max123";

        await board.Flip(playerId, 0, 0); // A - FirstCard
        await board.Flip(playerId, 0, 2); // A - SecondCard (match)

        await board.Flip(playerId, 0, 1); // B - FirstCard

        // Rule 2-A: No card there on second flip
        await Assert.ThrowsAsync<NoCardAtPositionException>(() =>
            board.Flip(playerId, 0, 0)); // try select as SecondCard an empty space

        var boardState = await board.ViewBy(playerId);

        Assert.Equal("none", SpotAt(boardState, 0, 0)); // empty
        Assert.Equal("up B", SpotAt(boardState, 0, 1)); // given up first card - B
        Assert.Equal("none", SpotAt(boardState, 0, 2)); // empty
    }

    /// <summary>
    /// Tests Rule 2-B: When trying to flip a card controlled by another player as the second card,
    /// throws CardAlreadyControlledException and player loses control of their first card.
    /// </summary>
    [Fact]
    public async Task Rule2B_Given_SelectFacingUpAlreadyControlledCard_When_FlipSecondCard_Then_ThrowsFlipExceptionAndGiveUpControlFirstCard()
    {
        // Arrange: Player1 creates matched pair, Player2 flips first card
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match, both controlled)
        var boardStateBeforeFirst = await board.ViewBy(firstPlayerId);

        await board.Flip(secondPlayerId, 0, 4); // A - FirstCard P2
        var boardStateBeforeSecond = await board.ViewBy(secondPlayerId);

        // Act & Assert: Rule 2-B - Cannot flip controlled card as second card
        await Assert.ThrowsAsync<CardAlreadyControlledException>(() =>
            board.Flip(secondPlayerId, 0, 0));  // A - P2 try open as SecondCard the controlled card of P1

        // Assert: Player2 loses control of first card, Player1 retains matched pair
        var boardStateAfterFirst = await board.ViewBy(firstPlayerId);
        var boardStateAfterSecond = await board.ViewBy(secondPlayerId);

        Assert.Equal("my A", SpotAt(boardStateBeforeFirst, 0, 0)); // first controls first card - A
        Assert.Equal("my A", SpotAt(boardStateBeforeFirst, 0, 2)); // first controls second card - A
        Assert.Equal("my A", SpotAt(boardStateBeforeSecond, 0, 4)); // second controls first card - A
        Assert.Equal("up A", SpotAt(boardStateBeforeSecond, 0, 0)); // second views card up - A
        Assert.Equal("up A", SpotAt(boardStateBeforeSecond, 0, 2)); // second views card up - A

        Assert.Equal("my A", SpotAt(boardStateAfterFirst, 0, 0)); // first still controls first card - A
        Assert.Equal("my A", SpotAt(boardStateAfterFirst, 0, 2)); // first still controls second card - A
        Assert.Equal("up A", SpotAt(boardStateAfterSecond, 0, 4)); // second given up first card - A
        Assert.Equal("up A", SpotAt(boardStateAfterSecond, 0, 0)); // second views card up - A
        Assert.Equal("up A", SpotAt(boardStateAfterSecond, 0, 2)); // second views card up - A
    }

    /// <summary>
    /// Tests Rule 2-B: When trying to flip your own controlled first card as the second card,
    /// throws CardAlreadyControlledException and player loses control of the first card.
    /// </summary>
    [Fact]
    public async Task Rule2B_Given_SelectOwnControlledCard_When_FlipSecondCard_Then_ThrowsFlipExceptionAndGiveUpControlFirstCard()
    {
        // Arrange: Player flips first card
        var board = await LoadBoard();
        var playerId = "max123";

        await board.Flip(playerId, 0, 0); // A - FirstCard
        var boardStateFlipFirst = await board.ViewBy(playerId);

        // Act & Assert: Rule 2-B - Cannot flip your own controlled first card as second card
        await Assert.ThrowsAsync<CardAlreadyControlledException>(() =>
            board.Flip(playerId, 0, 0)); // A - try open as SecondCard its own controlled FirstCard

        // Assert: Player loses control of the card
        var boardStateFlipSecond = await board.ViewBy(playerId);

        Assert.Equal("my A", SpotAt(boardStateFlipFirst, 0, 0)); // controls first card - A
        Assert.Equal("up A", SpotAt(boardStateFlipSecond, 0, 0)); // given up first card - A
    }

    /// <summary>
    /// Tests Rule 2-C: When flipping a face-down card as the second card,
    /// the card turns face up for all players and the flipping player gains control.
    /// </summary>
    [Fact]
    public async Task Rule2C_Given_SelectFacingDownCard_When_FlipSecondCard_Then_CardControlAndRemainsFaceUpForEveryone()
    {
        // Arrange: Player flips first card, second card still face down
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var boardStateBeforeFlipFirst = await board.ViewBy(firstPlayerId);
        var boardStateBeforeFlipSecond = await board.ViewBy(secondPlayerId);

        // Act: Rule 2-C - Flip face-down card as second card
        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)

        // Assert: Second card now face up and controlled, visible to all players
        var boardStateAfterFlipFirst = await board.ViewBy(firstPlayerId);
        var boardStateAfterFlipSecond = await board.ViewBy(secondPlayerId);

        Assert.Equal("my A", SpotAt(boardStateBeforeFlipFirst, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(boardStateBeforeFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("down", SpotAt(boardStateBeforeFlipFirst, 0, 2)); // first views card down - A
        Assert.Equal("down", SpotAt(boardStateBeforeFlipSecond, 0, 2)); // second views card down - A

        Assert.Equal("my A", SpotAt(boardStateAfterFlipFirst, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(boardStateAfterFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("my A", SpotAt(boardStateAfterFlipFirst, 0, 2)); // first controls second card - A
        Assert.Equal("up A", SpotAt(boardStateAfterFlipSecond, 0, 2)); // second views card up - A
    }

    /// <summary>
    /// Tests Rule 2-D: When flipping a matching second card,
    /// both cards remain controlled by the player and face up for all players.
    /// </summary>
    [Fact]
    public async Task Rule2D_Given_SelectMatchingCard_When_FlipSecondCard_Then_BothCardsControlAndRemainFaceUpForEveryone()
    {
        // Arrange: Player flips first card
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var boardStateBeforeFlipSecond = await board.ViewBy(secondPlayerId);
        var boardStateBeforeFlipFirst = await board.ViewBy(firstPlayerId);

        // Act: Rule 2-D - Flip matching second card
        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)

        // Assert: Player controls both matched cards, both face up for everyone
        var boardStateAfterFlipSecond = await board.ViewBy(secondPlayerId);
        var boardStateAfterFlipFirst = await board.ViewBy(firstPlayerId);

        Assert.Equal("my A", SpotAt(boardStateBeforeFlipFirst, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(boardStateBeforeFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("down", SpotAt(boardStateBeforeFlipFirst, 0, 2)); // first views card down - A
        Assert.Equal("down", SpotAt(boardStateBeforeFlipSecond, 0, 2)); // second views card down - A

        Assert.Equal("my A", SpotAt(boardStateAfterFlipFirst, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(boardStateAfterFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("my A", SpotAt(boardStateAfterFlipFirst, 0, 2)); // first controls second card - A (matched)
        Assert.Equal("up A", SpotAt(boardStateAfterFlipSecond, 0, 2)); // second views card up - A (matched)
    }

    /// <summary>
    /// Tests Rule 2-E: When flipping a non-matching second card,
    /// player loses control of both cards but they remain face up for all players.
    /// </summary>
    [Fact]
    public async Task Rule2E_Given_SelectNonMatchingCard_When_FlipSecondCard_Then_BothCardsGiveUpControlAndRemainFaceUpForEveryone()
    {
        // Arrange: Player flips first card
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var boardStateBeforeFlipFirst = await board.ViewBy(firstPlayerId);
        var boardStateBeforeFlipSecond = await board.ViewBy(secondPlayerId);

        // Act: Rule 2-E - Flip non-matching second card
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)
        var boardStateAfterFlipFirst = await board.ViewBy(firstPlayerId);

        // Act: Second player can now flip the released card
        await board.Flip(secondPlayerId, 0, 1); // B - FirstCard P2

        // Assert: First player loses control, both cards remain face up
        var boardStateAfterFlipSecond = await board.ViewBy(secondPlayerId);

        Assert.Equal("my A", SpotAt(boardStateBeforeFlipFirst, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(boardStateBeforeFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("down", SpotAt(boardStateBeforeFlipFirst, 0, 1)); // first views card down - B
        Assert.Equal("down", SpotAt(boardStateBeforeFlipSecond, 0, 1)); // second views card down - B

        Assert.Equal("up A", SpotAt(boardStateAfterFlipFirst, 0, 0)); // first views card up - A (no control)
        Assert.Equal("up A", SpotAt(boardStateAfterFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("up B", SpotAt(boardStateAfterFlipFirst, 0, 1)); // first views card up - B (no control)
        Assert.Equal("my B", SpotAt(boardStateAfterFlipSecond, 0, 1)); // second controls first card - B
    }

    /// <summary>
    /// Tests Rule 3-A: When flipping a new first card after a matched pair,
    /// the matched pair is removed from the board (cleanup).
    /// </summary>
    [Fact]
    public async Task Rule3A_Given_MatchedPair_When_FlipNewFirstCard_Then_RemovesPreviousMatchedCards()
    {
        // Arrange: Create matched pair
        var board = await LoadBoard();
        var playerId = "max123";

        await board.Flip(playerId, 0, 0); // A - FirstCard
        await board.Flip(playerId, 0, 2); // A - SecondCard (matched)

        var beforeCleanup = await board.ViewBy(playerId);

        // Act: Rule 3-A - Flip new first card triggers cleanup of matched pair
        await board.Flip(playerId, 0, 1); // B - FirstCard

        // Assert: Matched cards removed, new first card controlled
        var afterCleanup = await board.ViewBy(playerId);

        Assert.Equal("my A", SpotAt(beforeCleanup, 0, 0)); // controls first card - A
        Assert.Equal("my A", SpotAt(beforeCleanup, 0, 2)); // controls second card - A (matched)
        Assert.Equal("none", SpotAt(afterCleanup, 0, 0)); // empty (removed)
        Assert.Equal("my B", SpotAt(afterCleanup, 0, 1)); // controls first card - B (new)
        Assert.Equal("none", SpotAt(afterCleanup, 0, 2)); // empty (removed)
    }


    /// <summary>
    /// Tests Rule 3-B: When flipping a new first card after a non-matched pair,
    /// the non-matched cards turn face down for all players (cleanup).
    /// </summary>
    [Fact]
    public async Task Rule3B_Given_NonMatchedPair_When_FlipNewFirstCard_Then_TurnsPreviousCardsFaceDown()
    {
        // Arrange: Create non-matched pair
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)

        var beforeCleanup = await board.ViewBy(secondPlayerId);

        // Act: Rule 3-B - Flip new first card triggers cleanup of non-matched pair
        await board.Flip(firstPlayerId, 0, 2); // A - FirstCard P1

        // Assert: Non-matched cards turn face down, new first card face up
        var afterCleanup = await board.ViewBy(secondPlayerId);

        Assert.Equal("up A", SpotAt(beforeCleanup, 0, 0)); // second views card up - A
        Assert.Equal("up B", SpotAt(beforeCleanup, 0, 1)); // second views card up - B
        Assert.Equal("down", SpotAt(afterCleanup, 0, 0)); // second views card down - A (cleanup)
        Assert.Equal("down", SpotAt(afterCleanup, 0, 1)); // second views card down - B (cleanup)
        Assert.Equal("up A", SpotAt(afterCleanup, 0, 2)); // second views card up - A (new first card)
    }
}

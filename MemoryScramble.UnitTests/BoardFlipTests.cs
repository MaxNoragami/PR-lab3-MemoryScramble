using MemoryScramble.API.Exceptions;
using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

public class BoardFlipTests
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
    public async Task Rule1A_Given_EmptySpace_When_FlipFirstCard_Then_ThrowsFlipException()
    {
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)
        await board.Flip(firstPlayerId, 0, 1); // B - FirstCard P1

        // Rule 1-A: No card there on first flip
        await Assert.ThrowsAsync<NoCardAtPositionException>(() =>
            board.Flip(secondPlayerId, 0, 0)); // P2 try select as FirstCard an empty space

        var boardState = await board.ViewBy(secondPlayerId);
        Assert.Equal("none", SpotAt(boardState, 0, 0)); // empty
        Assert.Equal("none", SpotAt(boardState, 0, 2)); // empty
    }

    [Fact]
    public async Task Rule1B_Given_SelectFacingDownCard_When_FlipFirstCard_Then_CardControlAndFacesUpForEveryone()
    {
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        // Rule 1-B: Turn the card face up for everyone and control it
        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1

        var firstBoardState = await board.ViewBy(firstPlayerId);
        var secondBoardState = await board.ViewBy(secondPlayerId);

        Assert.Equal("my A", SpotAt(firstBoardState, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(secondBoardState, 0, 0)); // second views card up - A
    }

    [Fact]
    public async Task Rule1C_Given_SelectFacingDownCard_When_FlipFirstCard_Then_CardControlAndRemainsFaceUpForEveryone()
    {
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)
        var boardStateBeforeFirst = await board.ViewBy(firstPlayerId);
        var boardStateBeforeSecond = await board.ViewBy(secondPlayerId);

        // Rule 1-C: Player takes control, as card is already face up and not controlled
        await board.Flip(secondPlayerId, 0, 1); // B - FirstCard P2

        var firstBoardStateAfter = await board.ViewBy(firstPlayerId);
        var secondBoardStateAfter = await board.ViewBy(secondPlayerId);

        Assert.Equal("up B", SpotAt(boardStateBeforeFirst, 0, 1)); // first views card up - B
        Assert.Equal("up B", SpotAt(boardStateBeforeSecond, 0, 1)); // second views card up - B
        Assert.Equal("up B", SpotAt(firstBoardStateAfter, 0, 1)); // first views card up - B
        Assert.Equal("my B", SpotAt(secondBoardStateAfter, 0, 1)); // second controls first card - B
    }

    // TODO - Change from exception to waiting
    [Fact]
    public async Task Rule1D_Given_SelectAlreadyControlledCard_When_FlipFirstCard_Then_ThrowsFlipException()
    {
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        // Rule 1-D: Card is controlled by another dude, so it's waiting
        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var p2FlipTask = board.Flip(secondPlayerId, 0, 0); // A - FirstCard P1 (on holding)

        await Task.Delay(50);
        Assert.False(p2FlipTask.IsCompleted);

        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)
        await p2FlipTask;
        var boardStateSecond = await board.ViewBy(secondPlayerId);

        var boardStateFirst = await board.ViewBy(firstPlayerId);

        Assert.Equal("up A", SpotAt(boardStateFirst, 0, 0)); // first views card up - A
        Assert.Equal("my A", SpotAt(boardStateSecond, 0, 0)); // second controls first card - A
    }

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

    [Fact]
    public async Task Rule2B_Given_SelectFacingUpAlreadyControlledCard_When_FlipSecondCard_Then_ThrowsFlipExceptionAndGiveUpControlFirstCard()
    {
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)
        var boardStateBeforeFirst = await board.ViewBy(firstPlayerId);

        await board.Flip(secondPlayerId, 0, 4); // A - FirstCard P2
        var boardStateBeforeSecond = await board.ViewBy(secondPlayerId);

        // Rule 2-B: No waiting on second card, cannot select an already controlled card
        await Assert.ThrowsAsync<CardAlreadyControlledException>(() =>
            board.Flip(secondPlayerId, 0, 0));  // A - P2 try open as SecondCard the controlled FirstCard of P1

        var boardStateAfterFirst = await board.ViewBy(firstPlayerId);
        var boardStateAfterSecond = await board.ViewBy(secondPlayerId);

        Assert.Equal("my A", SpotAt(boardStateBeforeFirst, 0, 0)); // first controls first card - A
        Assert.Equal("my A", SpotAt(boardStateBeforeFirst, 0, 2)); // first controls second card - A
        Assert.Equal("my A", SpotAt(boardStateBeforeSecond, 0, 4)); // second controls first card - A
        Assert.Equal("up A", SpotAt(boardStateBeforeSecond, 0, 0)); // second views card up - A
        Assert.Equal("up A", SpotAt(boardStateBeforeSecond, 0, 2)); // second views card up - A

        Assert.Equal("my A", SpotAt(boardStateAfterFirst, 0, 0)); // first controls first card - A
        Assert.Equal("my A", SpotAt(boardStateAfterFirst, 0, 2)); // first controls second card - A
        Assert.Equal("up A", SpotAt(boardStateAfterSecond, 0, 4)); // second given up first card - A
        Assert.Equal("up A", SpotAt(boardStateAfterSecond, 0, 0)); // second views card up - A
        Assert.Equal("up A", SpotAt(boardStateAfterSecond, 0, 2)); // second views card up - A
    }

    [Fact]
    public async Task Rule2B_Given_SelectOwnControlledCard_When_FlipSecondCard_Then_ThrowsFlipExceptionAndGiveUpControlFirstCard()
    {
        var board = await LoadBoard();
        var playerId = "max123";

        await board.Flip(playerId, 0, 0); // A - FirstCard
        var boardStateFlipFirst = await board.ViewBy(playerId);

        // Rule 2-B: Can't flip your own FirstCard as SecondCard
        await Assert.ThrowsAsync<CardAlreadyControlledException>(() =>
            board.Flip(playerId, 0, 0)); // A - try open as SecondCard its own controlled FirstCard

        var boardStateFlipSecond = await board.ViewBy(playerId);

        Assert.Equal("my A", SpotAt(boardStateFlipFirst, 0, 0)); // controls first card - A
        Assert.Equal("up A", SpotAt(boardStateFlipSecond, 0, 0)); // given up first - A
    }

    [Fact]
    public async Task Rule2C_Given_SelectFacingDownCard_When_FlipSecondCard_Then_CardControlAndRemainsFaceUpForEveryone()
    {
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        // Rule 2-C: Turn cards facing down to facing up
        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var boardStateBeforeFlipFirst = await board.ViewBy(firstPlayerId);
        var boardStateBeforeFlipSecond = await board.ViewBy(secondPlayerId);

        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)
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

    [Fact]
    public async Task Rule2D_Given_SelectMatchingCard_When_FlipSecondCard_Then_BothCardsControlAndRemainFaceUpForEveryone()
    {
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var boardStateBeforeFlipSecond = await board.ViewBy(secondPlayerId);
        var boardStateBeforeFlipFirst = await board.ViewBy(firstPlayerId);

        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)
        var boardStateAfterFlipSecond = await board.ViewBy(secondPlayerId);
        var boardStateAfterFlipFirst = await board.ViewBy(firstPlayerId);

        Assert.Equal("my A", SpotAt(boardStateBeforeFlipFirst, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(boardStateBeforeFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("down", SpotAt(boardStateBeforeFlipFirst, 0, 2)); // first views card down - A
        Assert.Equal("down", SpotAt(boardStateBeforeFlipSecond, 0, 2)); // second views card down - A

        Assert.Equal("my A", SpotAt(boardStateAfterFlipFirst, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(boardStateAfterFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("my A", SpotAt(boardStateAfterFlipFirst, 0, 2)); // first controls second card - A
        Assert.Equal("up A", SpotAt(boardStateAfterFlipSecond, 0, 2)); // second views card up - A
    }

    [Fact]
    public async Task Rule2E_Given_SelectNonMatchingCard_When_FlipSecondCard_Then_BothCardsGiveUpControlAndRemainFaceUpForEveryone()
    {
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var boardStateBeforeFlipFirst = await board.ViewBy(firstPlayerId);
        var boardStateBeforeFlipSecond = await board.ViewBy(secondPlayerId);

        // Rule 2-E: No match => give up control of both cards, they remain face up
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)
        var boardStateAfterFlipFirst = await board.ViewBy(firstPlayerId);

        await board.Flip(secondPlayerId, 0, 1); // B - FirstCard P2
        var boardStateAfterFlipSecond = await board.ViewBy(secondPlayerId);

        Assert.Equal("my A", SpotAt(boardStateBeforeFlipFirst, 0, 0)); // first controls first card - A
        Assert.Equal("up A", SpotAt(boardStateBeforeFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("down", SpotAt(boardStateBeforeFlipFirst, 0, 1)); // first views card down - B
        Assert.Equal("down", SpotAt(boardStateBeforeFlipSecond, 0, 1)); // second views card down - B

        Assert.Equal("up A", SpotAt(boardStateAfterFlipFirst, 0, 0)); // first views card up - A
        Assert.Equal("up A", SpotAt(boardStateAfterFlipSecond, 0, 0)); // second views card up - A
        Assert.Equal("up B", SpotAt(boardStateAfterFlipFirst, 0, 1)); // first views card up - B
        Assert.Equal("my B", SpotAt(boardStateAfterFlipSecond, 0, 1)); // second controls first card - B
    }

    [Fact]
    public async Task Rule3A_Given_MatchedPair_When_FlipNewFirstCard_Then_RemovesPreviousMatchedCards()
    {
        var board = await LoadBoard();
        var playerId = "max123";

        await board.Flip(playerId, 0, 0); // A - FirstCard
        await board.Flip(playerId, 0, 2); // A - SecondCard (matched)

        var beforeCleanup = await board.ViewBy(playerId);

        // Rule 3-A: Flipping new first card removes matched pair
        await board.Flip(playerId, 0, 1); // B - FirstCard

        var afterCleanup = await board.ViewBy(playerId);

        Assert.Equal("my A", SpotAt(beforeCleanup, 0, 0)); // controls first card - A
        Assert.Equal("my A", SpotAt(beforeCleanup, 0, 2)); // controls second card - A
        Assert.Equal("none", SpotAt(afterCleanup, 0, 0)); // empty
        Assert.Equal("my B", SpotAt(afterCleanup, 0, 1)); // controls first card - B
        Assert.Equal("none", SpotAt(afterCleanup, 0, 2)); // empty
    }


    [Fact]
    public async Task Rule3B_Given_NonMatchedPair_When_FlipNewFirstCard_Then_TurnsPreviousCardsFaceDown()
    {
        var board = await LoadBoard();
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)

        var beforeCleanup = await board.ViewBy(secondPlayerId);

        // Rule 3-B: Flipping new first card turns non-matched cards face down
        await board.Flip(firstPlayerId, 0, 2); // A - FirstCard P1

        var afterCleanup = await board.ViewBy(secondPlayerId);

        Assert.Equal("up A", SpotAt(beforeCleanup, 0, 0)); // second views card up - A
        Assert.Equal("up B", SpotAt(beforeCleanup, 0, 1)); // second views card up - B
        Assert.Equal("down", SpotAt(afterCleanup, 0, 0)); // second views card down - A
        Assert.Equal("down", SpotAt(afterCleanup, 0, 1)); // second views card down - B
        Assert.Equal("up A", SpotAt(afterCleanup, 0, 2)); // second views card up - A
    }
}

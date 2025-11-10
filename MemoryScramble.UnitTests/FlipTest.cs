using MemoryScramble.API.Exceptions;
using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

public class FlipTest
{
    [Fact]
    public async Task Rule1A_Given_EmptySpace_When_FlipFirstCard_Then_ThrowsFlipException()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)
        await board.Flip(firstPlayerId, 0, 1); // B - FirstCard P1

        // Rule 1-A: No card there on first flip
        var exception = await Assert.ThrowsAsync<FlipException>(() =>
            board.Flip(secondPlayerId, 0, 0)); // P2 try select as FirstCard an empty space

        Assert.Contains("No card at that position", exception.Message);

        var boardState = board.ViewBy(secondPlayerId);
        var lines = boardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("none", lines[1]); // empty
        Assert.Equal("none", lines[3]); // empty
    }

    [Fact]
    public async Task Rule1B_Given_SelectFacingDownCard_When_FlipFirstCard_Then_CardControlAndFacesUpForEveryone()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        // Rule 1-B: Turn the card face up for everyone and control it
        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1

        var firstBoardState = board.ViewBy(firstPlayerId);
        var firstLines = firstBoardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var secondBoardState = board.ViewBy(secondPlayerId);
        var secondLines = secondBoardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("my A", firstLines[1]); // first controls first card - A
        Assert.Equal("up A", secondLines[1]); // second views card up - A
    }

    [Fact]
    public async Task Rule1C_Given_SelectFacingDownCard_When_FlipFirstCard_Then_CardControlAndRemainsFaceUpForEveryone()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)
        var boardStateBeforeFirst = board.ViewBy(firstPlayerId);
        var boardStateBeforeSecond = board.ViewBy(secondPlayerId);

        // Rule 1-C: Player takes control, as card is already face up and not controlled
        await board.Flip(secondPlayerId, 0, 1); // B - FirstCard P2

        var firstBoardStateAfter = board.ViewBy(firstPlayerId);
        var secondBoardStateAfter = board.ViewBy(secondPlayerId);
        var beforeLinesFirst = boardStateBeforeFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var beforeLinesSecond = boardStateBeforeSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterLinesFirst = firstBoardStateAfter.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterLinesSecond = secondBoardStateAfter.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("up B", beforeLinesFirst[2]); // first views card up - B
        Assert.Equal("up B", beforeLinesSecond[2]); // second views card up - B
        Assert.Equal("up B", afterLinesFirst[2]); // first views card up - B
        Assert.Equal("my B", afterLinesSecond[2]); // second controls first card - B
    }

    // TODO - Change from exception to waiting
    [Fact]
    public async Task Rule1D_Given_SelectAlreadyControlledCard_When_FlipFirstCard_Then_ThrowsFlipException()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        // Rule 1-D: Card is controlled by another dude, so it's waiting
        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var p2FlipTask = board.Flip(secondPlayerId, 0, 0); // A - FirstCard P1 (on holding)

        await Task.Delay(50);
        Assert.False(p2FlipTask.IsCompleted);

        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)
        await p2FlipTask;
        var boardStateSecond = board.ViewBy(secondPlayerId);

        var boardStateFirst = board.ViewBy(firstPlayerId);
        var linesFirst = boardStateFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var linesSecond = boardStateSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("up A", linesFirst[1]); // first views card up - A
        Assert.Equal("my A", linesSecond[1]); // second controls first card - A
    }

    [Fact]
    public async Task Rule2A_Given_EmptySpace_When_FlipSecondCard_Then_ThrowsFlipExceptionAndGiveUpControlFirstCard()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var playerId = "max123";

        await board.Flip(playerId, 0, 0); // A - FirstCard
        await board.Flip(playerId, 0, 2); // A - SecondCard (match)

        await board.Flip(playerId, 0, 1); // B - FirstCard

        // Rule 2-A: No card there on second flip
        var exception = await Assert.ThrowsAsync<FlipException>(() =>
            board.Flip(playerId, 0, 0)); // try select as SecondCard an empty space

        Assert.Contains("No card at that position", exception.Message);

        var boardState = board.ViewBy(playerId);
        var lines = boardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("none", lines[1]); // empty
        Assert.Equal("up B", lines[2]); // given up first card - B
        Assert.Equal("none", lines[3]); // empty
    }

    [Fact]
    public async Task Rule2B_Given_SelectFacingUpAlreadyControlledCard_When_FlipSecondCard_Then_ThrowsFlipExceptionAndGiveUpControlFirstCard()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)
        var boardStateBeforeFirst = board.ViewBy(firstPlayerId);

        await board.Flip(secondPlayerId, 0, 4); // A - FirstCard P2
        var boardStateBeforeSecond = board.ViewBy(secondPlayerId);

        // Rule 2-B: No waiting on second card, cannot select an already controlled card
        var exception = await Assert.ThrowsAsync<FlipException>(() =>
            board.Flip(secondPlayerId, 0, 0));  // A - P2 try open as SecondCard the controlled FirstCard of P1

        Assert.Contains("Card is already controlled", exception.Message);

        var boardStateAfterFirst = board.ViewBy(firstPlayerId);
        var boardStateAfterSecond = board.ViewBy(secondPlayerId);
        var beforeLinesFirst = boardStateBeforeFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var beforeLinesSecond = boardStateBeforeSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterLinesFirst = boardStateAfterFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterLinesSecond = boardStateAfterSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("my A", beforeLinesFirst[1]); // first controls first card - A
        Assert.Equal("my A", beforeLinesFirst[3]); // first controls second card - A
        Assert.Equal("my A", beforeLinesSecond[5]); // second controls first card - A
        Assert.Equal("up A", beforeLinesSecond[1]); // second views card up - A
        Assert.Equal("up A", beforeLinesSecond[3]); // second views card up - A

        Assert.Equal("my A", afterLinesFirst[1]); // first controls first card - A
        Assert.Equal("my A", afterLinesFirst[3]); // first controls second card - A
        Assert.Equal("up A", afterLinesSecond[5]); // second given up first card - A
        Assert.Equal("up A", afterLinesSecond[1]); // second views card up - A
        Assert.Equal("up A", afterLinesSecond[3]); // second views card up - A
    }

    [Fact]
    public async Task Rule2B_Given_SelectOwnControlledCard_When_FlipSecondCard_Then_ThrowsFlipExceptionAndGiveUpControlFirstCard()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var playerId = "max123";

        await board.Flip(playerId, 0, 0); // A - FirstCard
        var boardStateFlipFirst = board.ViewBy(playerId);

        // Rule 2-B: Can't flip your own FirstCard as SecondCard
        var exception = await Assert.ThrowsAsync<FlipException>(() =>
            board.Flip(playerId, 0, 0)); // A - try open as SecondCard its own controlled FirstCard

        Assert.Contains("Card is already controlled", exception.Message);

        var boardStateFlipSecond = board.ViewBy(playerId);
        var linesFirst = boardStateFlipFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var linesSecond = boardStateFlipSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("my A", linesFirst[1]); // controls first card - A
        Assert.Equal("up A", linesSecond[1]); // given up first - A
    }

    [Fact]
    public async Task Rule2C_Given_SelectFacingDownCard_When_FlipSecondCard_Then_CardControlAndRemainsFaceUpForEveryone()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        // Rule 2-C: Turn cards facing down to facing up
        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var boardStateBeforeFlipFirst = board.ViewBy(firstPlayerId);
        var boardStateBeforeFlipSecond = board.ViewBy(secondPlayerId);

        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)
        var boardStateAfterFlipFirst = board.ViewBy(firstPlayerId);
        var boardStateAfterFlipSecond = board.ViewBy(secondPlayerId);

        var beforeFlipLinesFirst = boardStateBeforeFlipFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var beforeFlipLinesSecond = boardStateBeforeFlipSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterFlipLinesFirst = boardStateAfterFlipFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterFlipLinesSecond = boardStateAfterFlipSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("my A", beforeFlipLinesFirst[1]); // first controls first card - A
        Assert.Equal("up A", beforeFlipLinesSecond[1]); // second views card up - A
        Assert.Equal("down", beforeFlipLinesFirst[3]); // first views card down - A
        Assert.Equal("down", beforeFlipLinesSecond[3]); // second views card down - A

        Assert.Equal("my A", afterFlipLinesFirst[1]); // first controls first card - A
        Assert.Equal("up A", afterFlipLinesSecond[1]); // second views card up - A
        Assert.Equal("my A", afterFlipLinesFirst[3]); // first controls second card - A
        Assert.Equal("up A", afterFlipLinesSecond[3]); // second views card up - A
    }

    [Fact]
    public async Task Rule2D_Given_SelectMatchingCard_When_FlipSecondCard_Then_BothCardsControlAndRemainFaceUpForEveryone()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var boardStateBeforeFlipSecond = board.ViewBy(secondPlayerId);
        var boardStateBeforeFlipFirst = board.ViewBy(firstPlayerId);

        await board.Flip(firstPlayerId, 0, 2); // A - SecondCard P1 (match)
        var boardStateAfterFlipSecond = board.ViewBy(secondPlayerId);
        var boardStateAfterFlipFirst = board.ViewBy(firstPlayerId);

        var beforeFlipLinesFirst = boardStateBeforeFlipFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var beforeFlipLinesSecond = boardStateBeforeFlipSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterFlipLinesFirst = boardStateAfterFlipFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterFlipLinesSecond = boardStateAfterFlipSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("my A", beforeFlipLinesFirst[1]); // first controls first card - A
        Assert.Equal("up A", beforeFlipLinesSecond[1]); // second views card up - A
        Assert.Equal("down", beforeFlipLinesFirst[3]); // first views card down - A
        Assert.Equal("down", beforeFlipLinesSecond[3]); // second views card down - A

        Assert.Equal("my A", afterFlipLinesFirst[1]); // first controls first card - A
        Assert.Equal("up A", afterFlipLinesSecond[1]); // second views card up - A
        Assert.Equal("my A", afterFlipLinesFirst[3]); // first controls second card - A
        Assert.Equal("up A", afterFlipLinesSecond[3]); // second views card up - A
    }

    [Fact]
    public async Task Rule2E_Given_SelectNonMatchingCard_When_FlipSecondCard_Then_BothCardsGiveUpControlAndRemainFaceUpForEveryone()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        var boardStateBeforeFlipFirst = board.ViewBy(firstPlayerId);
        var boardStateBeforeFlipSecond = board.ViewBy(secondPlayerId);

        // Rule 2-E: No match => give up control of both cards, they remain face up
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)
        var boardStateAfterFlipFirst = board.ViewBy(firstPlayerId);

        await board.Flip(secondPlayerId, 0, 1); // B - FirstCard P2
        var boardStateAfterFlipSecond = board.ViewBy(secondPlayerId);

        var beforeFlipLinesFirst = boardStateBeforeFlipFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var beforeFlipLinesSecond = boardStateBeforeFlipSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterFlipLinesFirst = boardStateAfterFlipFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterFlipLinesSecond = boardStateAfterFlipSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("my A", beforeFlipLinesFirst[1]); // first controls first card - A
        Assert.Equal("up A", beforeFlipLinesSecond[1]); // second views card up - A
        Assert.Equal("down", beforeFlipLinesFirst[2]); // first views card down - B
        Assert.Equal("down", beforeFlipLinesSecond[2]); // second views card down - B

        Assert.Equal("up A", afterFlipLinesFirst[1]); // first views card up - A
        Assert.Equal("up A", afterFlipLinesSecond[1]); // second views card up - A
        Assert.Equal("up B", afterFlipLinesFirst[2]); // first views card up - B
        Assert.Equal("my B", afterFlipLinesSecond[2]); // second controls first card - B
    }

    [Fact]
    public async Task Rule3A_Given_MatchedPair_When_FlipNewFirstCard_Then_RemovesPreviousMatchedCards()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var playerId = "max123";

        await board.Flip(playerId, 0, 0); // A - FirstCard
        await board.Flip(playerId, 0, 2); // A - SecondCard (matched)

        var beforeCleanup = board.ViewBy(playerId);

        // Rule 3-A: Flipping new first card removes matched pair
        await board.Flip(playerId, 0, 1); // B - FirstCard

        var afterCleanup = board.ViewBy(playerId);
        var beforeLines = beforeCleanup.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterLines = afterCleanup.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("my A", beforeLines[1]); // controls first card - A
        Assert.Equal("my A", beforeLines[3]); // controls second card - A
        Assert.Equal("none", afterLines[1]); // empty
        Assert.Equal("my B", afterLines[2]); // controls first card - B
        Assert.Equal("none", afterLines[3]); // empty
    }


    [Fact]
    public async Task Rule3B_Given_NonMatchedPair_When_FlipNewFirstCard_Then_TurnsPreviousCardsFaceDown()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstPlayerId = "max123";
        var secondPlayerId = "johnPork";

        await board.Flip(firstPlayerId, 0, 0); // A - FirstCard P1
        await board.Flip(firstPlayerId, 0, 1); // B - SecondCard P1 (no match)

        var beforeCleanup = board.ViewBy(secondPlayerId);

        // Rule 3-B: Flipping new first card turns non-matched cards face down
        await board.Flip(firstPlayerId, 0, 2); // A - FirstCard P1

        var afterCleanup = board.ViewBy(secondPlayerId);
        var beforeLines = beforeCleanup.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var afterLines = afterCleanup.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("up A", beforeLines[1]); // second views card up - A
        Assert.Equal("up B", beforeLines[2]); // second views card up - B
        Assert.Equal("down", afterLines[1]); // second views card down - A
        Assert.Equal("down", afterLines[2]); // second views card down - B
        Assert.Equal("up A", afterLines[3]); // second views card up - A
    }
}

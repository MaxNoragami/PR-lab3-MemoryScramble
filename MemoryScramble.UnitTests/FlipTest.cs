using MemoryScramble.API.Exceptions;
using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

public class FlipTest
{
    [Fact]
    public async Task Rule1A_Given_EmptySpace_When_FlipFirstCard_Then_ThrowsFlipException()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var playerId = "max123";

        board.Flip(playerId, 0, 0); // A
        board.Flip(playerId, 0, 2); // A

        // Rule 1-A: No card there on first flip
        var exception = Assert.Throws<FlipException>(() =>
            board.Flip(playerId, 0, 0));

        Assert.Contains("No card at that position", exception.Message);

        var boardState = board.ViewBy(playerId);
        var lines = boardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("none", lines[1]); // empty
        Assert.Equal("none", lines[3]); // empty
    }

    [Fact]
    public async Task Rule1B_Given_SelectFacingDownCard_When_FlipFirstCard_Then_CardControlAndFacesUpForEveryone()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var firstplayerId = "max123";
        var secondPlayerId = "johnPork";

        // Rule 1-B: Turn the card face up for everyone and control it
        board.Flip(firstplayerId, 0, 0); // A

        var firstBoardState = board.ViewBy(firstplayerId);
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

        board.Flip(firstPlayerId, 0, 0); // A
        var boardStateBeforeFirst = board.Flip(firstPlayerId, 0, 1); // B
        var boardStateBeforeSecond = board.ViewBy(secondPlayerId);

        // Rule 1-C: Player takes control, as card is already face up and not controlled
        board.Flip(secondPlayerId, 0, 1); // B

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

        board.Flip(firstPlayerId, 0, 0); // A
        board.Flip(firstPlayerId, 0, 2); // A

        // Rule 1-D: Card is controlled by another dude
        var exception = Assert.Throws<FlipException>(() =>
            board.Flip(secondPlayerId, 0, 0));

        Assert.Contains("Card is controlled by another player", exception.Message);

        var boardStateFirst = board.ViewBy(firstPlayerId);
        var boardStateSecond = board.ViewBy(secondPlayerId);
        var linesFirst = boardStateFirst.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var linesSecond = boardStateSecond.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("my A", linesFirst[1]); // first controls both cards - A
        Assert.Equal("up A", linesSecond[1]); // second views card up - A
    }

    [Fact]
    public async Task Rule2A_Given_EmptySpace_When_FlipSecondCard_Then_ThrowsFlipExceptionAndGiveUpControlFirstCard()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var playerId = "max123";

        board.Flip(playerId, 0, 0); // A
        board.Flip(playerId, 0, 2); // A

        board.Flip(playerId, 0, 1); // B

        // Rule 2-A: No card there on second flip
        var exception = Assert.Throws<FlipException>(() =>
            board.Flip(playerId, 0, 0));

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

        board.Flip(firstPlayerId, 0, 0); // A
        var boardStateBeforeFirst = board.Flip(firstPlayerId, 0, 2); // A
        var boardStateBeforeSecond = board.Flip(secondPlayerId, 0, 4); // A

        // Rule 2-B: No waiting on second card, cannot select an already controlled card
        var exception = Assert.Throws<FlipException>(() =>
            board.Flip(secondPlayerId, 0, 0));

        Assert.Contains("Card is controlled by another player", exception.Message);

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
        Assert.Equal("up A", afterLinesSecond[5]); // second given up first card - B
        Assert.Equal("up A", afterLinesSecond[1]); // second views card up - A
        Assert.Equal("up A", afterLinesSecond[3]); // second views card up - A
    }


}

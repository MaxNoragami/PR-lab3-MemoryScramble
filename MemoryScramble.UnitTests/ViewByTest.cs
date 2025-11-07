using MemoryScramble.API.Exceptions;
using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

public class ViewByTest
{
    [Fact]
    public async Task Given_InitialBoard_When_ViewBy_Then_ReturnsAllCardsDown()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var playerId = "max123";

        var boardState = board.ViewBy(playerId);

        var lines = boardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal(26, lines.Length);

        for (int i = 1; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    [Fact]
    public async Task Given_PlayerControlsCard_When_ViewBy_Then_ShowsMyCard()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var playerId = "max123";

        board.Flip(playerId, 0, 0);
        var boardState = board.ViewBy(playerId);

        var lines = boardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal("my A", lines[1]);

        for (int i = 2; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    [Fact]
    public async Task Given_AnotherPlayerControlsCard_When_ViewBy_Then_ShowsUpCard()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var player1 = "max123";
        var player2 = "johnPork";

        board.Flip(player1, 0, 0);
        var boardState = board.ViewBy(player2);

        var lines = boardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal("up A", lines[1]);

        for (int i = 2; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    [Fact]
    public async Task Given_MatchedPairRemoved_When_ViewBy_Then_ShowsNoneForRemovedCards()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var playerId = "max123";

        board.Flip(playerId, 0, 0);
        board.Flip(playerId, 0, 2);
        board.Flip(playerId, 0, 1);

        var boardState = board.ViewBy(playerId);

        var lines = boardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal("none", lines[1]);
        Assert.Equal("my B", lines[2]);
        Assert.Equal("none", lines[3]);

        for (int i = 4; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    [Fact]
    public async Task Given_ComplexBoardState_When_ViewBy_Then_ShowsCorrectMixedStates()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var player1 = "max123";
        var player2 = "johnPork";

        board.Flip(player1, 0, 0);
        board.Flip(player1, 0, 2);
        board.Flip(player1, 0, 1);
        board.Flip(player1, 0, 3);

        var player1State = board.ViewBy(player1);
        var player2State = board.ViewBy(player2);

        var lines1 = player1State.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("5x5", lines1[0]);
        Assert.Equal("none", lines1[1]);
        Assert.Equal("my B", lines1[2]);
        Assert.Equal("none", lines1[3]);
        Assert.Equal("my B", lines1[4]);

        var lines2 = player2State.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("5x5", lines2[0]);
        Assert.Equal("none", lines2[1]);
        Assert.Equal("up B", lines2[2]);
        Assert.Equal("none", lines2[3]);
        Assert.Equal("up B", lines2[4]);
    }

    [Fact]
    public async Task Given_NonMatchingCardsAfterSecondFlip_When_ViewBy_Then_ShowsBothCardsUp()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var player1 = "max123";
        var player2 = "johnPork";

        board.Flip(player1, 0, 0);
        board.Flip(player1, 0, 1);

        var player1State = board.ViewBy(player1);
        var player2State = board.ViewBy(player2);

        var lines1 = player1State.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("up A", lines1[1]);
        Assert.Equal("up B", lines1[2]);

        var lines2 = player2State.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("up A", lines2[1]);
        Assert.Equal("up B", lines2[2]);
    }

    [Fact]
    public async Task Given_EmptyPlayerId_When_ViewBy_Then_ThrowsArgumentException()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");

        Assert.Throws<ArgumentException>(() => board.ViewBy(""));
        Assert.Throws<ArgumentException>(() => board.ViewBy("   "));
        Assert.Throws<ArgumentException>(() => board.ViewBy(null!));
    }

    [Fact]
    public async Task Given_MultiplePlayersWithDifferentStates_When_ViewBy_Then_EachPlayerSeesCorrectState()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var player1 = "alice";
        var player2 = "bob";
        var player3 = "charlie";

        board.Flip(player1, 0, 0);
        board.Flip(player1, 1, 0);

        board.Flip(player2, 0, 2);
        board.Flip(player2, 1, 2);

        board.Flip(player3, 0, 1);

        var aliceState = board.ViewBy(player1);
        var bobState = board.ViewBy(player2);
        var charlieState = board.ViewBy(player3);

        var aliceLines = aliceState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("up A", aliceLines[1]);
        Assert.Equal("up B", aliceLines[2]);
        Assert.Equal("up A", aliceLines[3]);
        Assert.Equal("up B", aliceLines[6]);
        Assert.Equal("up B", aliceLines[8]);

        var bobLines = bobState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("up A", bobLines[1]);
        Assert.Equal("up B", bobLines[2]);
        Assert.Equal("up A", bobLines[3]);
        Assert.Equal("up B", bobLines[6]);
        Assert.Equal("up B", bobLines[8]);

        var charlieLines = charlieState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("up A", charlieLines[1]);
        Assert.Equal("my B", charlieLines[2]);
        Assert.Equal("up A", charlieLines[3]);
        Assert.Equal("up B", charlieLines[6]);
        Assert.Equal("up B", charlieLines[8]);
    }

    [Fact]
    public async Task Given_BoardStateAfterCleanup_When_ViewBy_Then_ShowsCorrectTurnedDownCards()
    {
        var board = await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
        var playerId = "max123";

        board.Flip(playerId, 0, 0);
        board.Flip(playerId, 0, 1);
        board.Flip(playerId, 0, 4);

        var boardState = board.ViewBy(playerId);

        var lines = boardState.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal("down", lines[1]);
        Assert.Equal("down", lines[2]); 
        Assert.Equal("my A", lines[5]);

        for (int i = 3; i <= 4; i++)
            Assert.Equal("down", lines[i]);
        for (int i = 6; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }
}

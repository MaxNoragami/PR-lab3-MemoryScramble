using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

public class BoardViewByTests
{
    private static async Task<Board> LoadBoard()
        => await Board.ParseFromFile("TestingBoards/Valid/5x5.txt");
    
    private static string[] GetLines(string boardState) =>
        boardState.Replace("\r", string.Empty)
                  .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string SpotAt(string boardState, int row, int col)
    {
        var lines = GetLines(boardState);
        return lines[1 + row * 5 + col];
    }

    [Fact]
    public async Task Given_InitialBoard_When_ViewBy_Then_ReturnsAllCardsDown()
    {
        var board = await LoadBoard();
        var playerId = "max123";

        var boardState = await board.ViewBy(playerId);

        var lines = GetLines(boardState);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal(26, lines.Length);

        for (int i = 1; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    [Fact]
    public async Task Given_PlayerControlsCard_When_ViewBy_Then_ShowsMyCard()
    {
        var board = await LoadBoard();
        var playerId = "max123";

        await board.Flip(playerId, 0, 0);
        var boardState = await board.ViewBy(playerId);

        var lines = GetLines(boardState);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal("my A", SpotAt(boardState, 0, 0));

        for (int i = 2; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    [Fact]
    public async Task Given_AnotherPlayerControlsCard_When_ViewBy_Then_ShowsUpCard()
    {
        var board = await LoadBoard();
        var player1 = "max123";
        var player2 = "johnPork";

        await board.Flip(player1, 0, 0);
        var boardState = await board.ViewBy(player2);

        var lines = GetLines(boardState);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal("up A", SpotAt(boardState, 0, 0));

        for (int i = 2; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    [Fact]
    public async Task Given_MatchedPairRemoved_When_ViewBy_Then_ShowsNoneForRemovedCards()
    {
        var board = await LoadBoard();
        var playerId = "max123";

        await board.Flip(playerId, 0, 0);
        await board.Flip(playerId, 0, 2);
        await board.Flip(playerId, 0, 1);

        var boardState = await board.ViewBy(playerId);

        var lines = GetLines(boardState);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal("none", SpotAt(boardState, 0, 0));
        Assert.Equal("my B", SpotAt(boardState, 0, 1));
        Assert.Equal("none", SpotAt(boardState, 0, 2));

        for (int i = 4; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }

    [Fact]
    public async Task Given_ComplexBoardState_When_ViewBy_Then_ShowsCorrectMixedStates()
    {
        var board = await LoadBoard();
        var player1 = "max123";
        var player2 = "johnPork";

        await board.Flip(player1, 0, 0);
        await board.Flip(player1, 0, 2);
        await board.Flip(player1, 0, 1);
        await board.Flip(player1, 0, 3);

        var player1State = await board.ViewBy(player1);
        var player2State = await board.ViewBy(player2);

        Assert.Equal("none", SpotAt(player1State, 0, 0));
        Assert.Equal("my B", SpotAt(player1State, 0, 1));
        Assert.Equal("none", SpotAt(player1State, 0, 2));
        Assert.Equal("my B", SpotAt(player1State, 0, 3));

        Assert.Equal("none", SpotAt(player2State, 0, 0));
        Assert.Equal("up B", SpotAt(player2State, 0, 1));
        Assert.Equal("none", SpotAt(player2State, 0, 2));
        Assert.Equal("up B", SpotAt(player2State, 0, 3));
    }

    [Fact]
    public async Task Given_NonMatchingCardsAfterSecondFlip_When_ViewBy_Then_ShowsBothCardsUp()
    {
        var board = await LoadBoard();
        var player1 = "max123";
        var player2 = "johnPork";

        await board.Flip(player1, 0, 0);
        await board.Flip(player1, 0, 1);

        var player1State = await board.ViewBy(player1);
        var player2State = await board.ViewBy(player2);

        Assert.Equal("up A", SpotAt(player1State, 0, 0));
        Assert.Equal("up B", SpotAt(player1State, 0, 1));

        Assert.Equal("up A", SpotAt(player2State, 0, 0));
        Assert.Equal("up B", SpotAt(player2State, 0, 1));
    }

    [Fact]
    public async Task Given_EmptyPlayerId_When_ViewBy_Then_ThrowsArgumentException()
    {
        var board = await LoadBoard();

        await Assert.ThrowsAsync<ArgumentException>(() => board.ViewBy(""));
        await Assert.ThrowsAsync<ArgumentException>(() => board.ViewBy("   "));
        await Assert.ThrowsAsync<ArgumentException>(() => board.ViewBy(null!));
    }

    [Fact]
    public async Task Given_MultiplePlayersWithDifferentStates_When_ViewBy_Then_EachPlayerSeesCorrectState()
    {
        var board = await LoadBoard();
        var player1 = "alice";
        var player2 = "bob";
        var player3 = "charlie";

        await board.Flip(player1, 0, 0);
        await board.Flip(player1, 1, 0);

        await board.Flip(player2, 0, 2);
        await board.Flip(player2, 1, 2);

        await board.Flip(player3, 0, 1);

        var aliceState = await board.ViewBy(player1);
        var bobState = await board.ViewBy(player2);
        var charlieState = await board.ViewBy(player3);

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

        Assert.Equal("up A", SpotAt(charlieState, 0, 0));
        Assert.Equal("my B", SpotAt(charlieState, 0, 1));
        Assert.Equal("up A", SpotAt(charlieState, 0, 2));
        Assert.Equal("up B", SpotAt(charlieState, 1, 0));
        Assert.Equal("up B", SpotAt(charlieState, 1, 2));
    }

    [Fact]
    public async Task Given_BoardStateAfterCleanup_When_ViewBy_Then_ShowsCorrectTurnedDownCards()
    {
        var board = await LoadBoard();
        var playerId = "max123";

        await board.Flip(playerId, 0, 0);
        await board.Flip(playerId, 0, 1);
        await board.Flip(playerId, 0, 4);

        var boardState = await board.ViewBy(playerId);

        var lines = GetLines(boardState);

        Assert.Equal("5x5", lines[0]);
        Assert.Equal("down", SpotAt(boardState, 0, 0));
        Assert.Equal("down", SpotAt(boardState, 0, 1)); 
        Assert.Equal("my A", SpotAt(boardState, 0, 4));

        for (int i = 3; i <= 4; i++)
            Assert.Equal("down", lines[i]);
        for (int i = 6; i < lines.Length; i++)
            Assert.Equal("down", lines[i]);
    }
}

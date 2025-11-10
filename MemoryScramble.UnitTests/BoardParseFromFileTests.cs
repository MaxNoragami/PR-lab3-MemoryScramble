using MemoryScramble.API.Exceptions;
using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

public class BoardParseFromFileTests
{
    [Theory]
    [InlineData("TestingBoards/WithInvalidSizeFormat/1.txt")]
    [InlineData("TestingBoards/WithInvalidSizeFormat/2.txt")]
    [InlineData("TestingBoards/WithInvalidSizeFormat/3.txt")]
    [InlineData("TestingBoards/WithInvalidSizeFormat/4.txt")]
    [InlineData("TestingBoards/WithInvalidSizeFormat/5.txt")]
    public async Task Given_BoardsWithInvalidSizeFormat_When_ParseFromFile_Then_ThrowsInvalidGridFormatException(string filePath)
    {
        var exception = await Assert.ThrowsAsync<InvalidGridFormatException>(async () =>
            await Board.ParseFromFile(filePath));

        Assert.Contains("The grid size format must match `RxC` with no spaces", exception.Message);
    }

    [Theory]
    [InlineData("TestingBoards/WithInvalidRowColumnValue/1.txt")]
    [InlineData("TestingBoards/WithInvalidRowColumnValue/2.txt")]
    public async Task Given_BoardsWithInvalidRowColumnValue_When_ParseFromFile_Then_ThrowsInvalidGridFormatException(string filePath)
    {
        var exception = await Assert.ThrowsAsync<InvalidGridFormatException>(async () =>
            await Board.ParseFromFile(filePath));

        Assert.Contains("amount must be an integer greater than zero", exception.Message);
    }

    [Theory]
    [InlineData("TestingBoards/WithMismatchedCardCount/1.txt")]
    [InlineData("TestingBoards/WithMismatchedCardCount/2.txt")]
    [InlineData("TestingBoards/WithMismatchedCardCount/3.txt")]
    public async Task Given_BoardsWithMismatchedCardCount_When_ParseFromFile_Then_ThrowsInvalidGridFormatException(string filePath)
    {
        var exception = await Assert.ThrowsAsync<InvalidGridFormatException>(async () =>
            await Board.ParseFromFile(filePath));

        Assert.Contains("The amount of cards do not match the specified grid size", exception.Message);
    }

    [Theory]
    [InlineData("TestingBoards/WithInvalidCards/1.txt")]
    [InlineData("TestingBoards/WithInvalidCards/2.txt")]
    [InlineData("TestingBoards/WithInvalidCards/3.txt")]
    [InlineData("TestingBoards/WithInvalidCards/4.txt")]
    public async Task Given_BoardsWithInvalidCards_When_ParseFromFile_Then_ThrowsInvalidGridFormatException(string filePath)
    {
        var exception = await Assert.ThrowsAsync<InvalidGridFormatException>(async () =>
            await Board.ParseFromFile(filePath));

        Assert.Contains("Invalid card", exception.Message);
        Assert.Contains("cards must be non-empty and contain no whitespace", exception.Message);
    }

    [Theory]
    [InlineData("TestingBoards/Valid/2x2.txt", 2, 2)]
    [InlineData("TestingBoards/Valid/3x3.txt", 3, 3)]
    [InlineData("TestingBoards/Valid/4x1.txt", 4, 1)]
    [InlineData("TestingBoards/Valid/1x1.txt", 1, 1)]
    public async Task Given_ValidBoardFiles_When_ParseFromFile_Then_CreatesCorrectBoard(string filePath, int expectedRows, int expectedColumns)
    {
        var board = await Board.ParseFromFile(filePath);

        Assert.NotNull(board);
        Assert.Equal(expectedRows, board.Rows);
        Assert.Equal(expectedColumns, board.Columns);

        var boardString = board.ToString();
        var lines = boardString.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal($"{expectedRows}x{expectedColumns}", lines[0]);

        Assert.Equal(expectedRows + 1, lines.Length);

        for (int i = 1; i <= expectedRows; i++)
        {
            var cardCount = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(expectedColumns, cardCount);
        }
    }

    [Fact]
    public async Task Given_InexistentBoardFiles_When_ParseFromFile_Then_CreatesCorrectBoard()
    {
        var inexistentPath = "TestingBoards/yolo.txt";
        
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await Board.ParseFromFile(inexistentPath));
    }
}

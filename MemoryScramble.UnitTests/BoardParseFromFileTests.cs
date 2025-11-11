using MemoryScramble.API.Exceptions;
using PR_lab3_MemoryScramble.API;

namespace MemoryScramble.UnitTests;

/// <summary>
/// Tests for Board.ParseFromFile() functionality - verifying board file parsing and validation.
/// </summary>
public class BoardParseFromFileTests
{
    /// <summary>
    /// Tests that board files with invalid size format (e.g., missing 'x', non-numeric values)
    /// throw InvalidGridSizeFormatException when parsed.
    /// </summary>
    [Theory]
    [InlineData("TestingBoards/WithInvalidSizeFormat/1.txt")]
    [InlineData("TestingBoards/WithInvalidSizeFormat/2.txt")]
    [InlineData("TestingBoards/WithInvalidSizeFormat/3.txt")]
    [InlineData("TestingBoards/WithInvalidSizeFormat/4.txt")]
    [InlineData("TestingBoards/WithInvalidSizeFormat/5.txt")]
    public async Task Given_BoardsWithInvalidSizeFormat_When_ParseFromFile_Then_ThrowsInvalidGridSizeFormatException(string filePath)
    {
        // Act & Assert: Parsing file with invalid size format throws exception
        await Assert.ThrowsAsync<InvalidGridSizeFormatException>(async () =>
            await Board.ParseFromFile(filePath));
    }

    /// <summary>
    /// Tests that board files with invalid row or column values (e.g., zero, negative)
    /// throw InvalidRowColumnValueException when parsed.
    /// </summary>
    [Theory]
    [InlineData("TestingBoards/WithInvalidRowColumnValue/1.txt")]
    [InlineData("TestingBoards/WithInvalidRowColumnValue/2.txt")]
    public async Task Given_BoardsWithInvalidRowColumnValue_When_ParseFromFile_Then_ThrowsInvalidRowColumnValueException(string filePath)
    {
        // Act & Assert: Parsing file with invalid row/column values throws exception
        await Assert.ThrowsAsync<InvalidRowColumnValueException>(async () =>
            await Board.ParseFromFile(filePath));
    }

    /// <summary>
    /// Tests that board files with mismatched card counts (incorrect number of cards per row,
    /// or card counts that don't match declared grid size) throw MismatchedCardCountException.
    /// </summary>
    [Theory]
    [InlineData("TestingBoards/WithMismatchedCardCount/1.txt")]
    [InlineData("TestingBoards/WithMismatchedCardCount/2.txt")]
    [InlineData("TestingBoards/WithMismatchedCardCount/3.txt")]
    public async Task Given_BoardsWithMismatchedCardCount_When_ParseFromFile_Then_ThrowsMismatchedCardCountException(string filePath)
    {
        // Act & Assert: Parsing file with mismatched card count throws exception
        await Assert.ThrowsAsync<MismatchedCardCountException>(async () =>
            await Board.ParseFromFile(filePath));
    }

    /// <summary>
    /// Tests that board files with invalid card formats (e.g., empty cards, invalid characters,
    /// cards with incorrect structure) throw InvalidCardFormatException when parsed.
    /// </summary>
    [Theory]
    [InlineData("TestingBoards/WithInvalidCards/1.txt")]
    [InlineData("TestingBoards/WithInvalidCards/2.txt")]
    [InlineData("TestingBoards/WithInvalidCards/3.txt")]
    [InlineData("TestingBoards/WithInvalidCards/4.txt")]
    public async Task Given_BoardsWithInvalidCards_When_ParseFromFile_Then_ThrowsInvalidCardFormatException(string filePath)
    {
        // Act & Assert: Parsing file with invalid card format throws exception
        await Assert.ThrowsAsync<InvalidCardFormatException>(async () =>
            await Board.ParseFromFile(filePath));
    }

    /// <summary>
    /// Tests that valid board files are parsed correctly, creating boards with the correct
    /// dimensions and card layout matching the declared grid size.
    /// </summary>
    [Theory]
    [InlineData("TestingBoards/Valid/2x2.txt", 2, 2)]
    [InlineData("TestingBoards/Valid/3x3.txt", 3, 3)]
    [InlineData("TestingBoards/Valid/4x1.txt", 4, 1)]
    [InlineData("TestingBoards/Valid/1x1.txt", 1, 1)]
    public async Task Given_ValidBoardFiles_When_ParseFromFile_Then_CreatesCorrectBoard(string filePath, int expectedRows, int expectedColumns)
    {
        // Act: Parse valid board file
        var board = await Board.ParseFromFile(filePath);

        // Assert: Board created with correct dimensions
        Assert.NotNull(board);
        Assert.Equal(expectedRows, board.Rows);
        Assert.Equal(expectedColumns, board.Columns);

        // Assert: Board string representation matches expected format
        var boardString = board.ToString();
        var lines = boardString.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal($"{expectedRows}x{expectedColumns}", lines[0]); // First line is size

        Assert.Equal(expectedRows + 1, lines.Length); // Size line + row lines

        // Assert: Each row has correct number of cards
        for (int i = 1; i <= expectedRows; i++)
        {
            var cardCount = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(expectedColumns, cardCount);
        }
    }

    /// <summary>
    /// Tests that attempting to parse a non-existent board file throws FileNotFoundException.
    /// </summary>
    [Fact]
    public async Task Given_InexistentBoardFiles_When_ParseFromFile_Then_ThrowsFileNotFoundException()
    {
        // Arrange: Specify non-existent file path
        var inexistentPath = "TestingBoards/yolo.txt";
        
        // Act & Assert: Parsing non-existent file throws FileNotFoundException
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await Board.ParseFromFile(inexistentPath));
    }
}

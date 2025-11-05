using MemoryScramble.API.Exceptions;
using PR_lab3_MemoryScramble;

namespace MemoryScramble.UnitTests;

public class BoardTest
{
    [Theory]
    [InlineData("TestingBoards/BoardsWithInvalidSizeFormat/1.txt")]
    [InlineData("TestingBoards/BoardsWithInvalidSizeFormat/2.txt")]
    [InlineData("TestingBoards/BoardsWithInvalidSizeFormat/3.txt")]
    [InlineData("TestingBoards/BoardsWithInvalidSizeFormat/4.txt")]
    [InlineData("TestingBoards/BoardsWithInvalidSizeFormat/5.txt")]
    public async Task Given_BoardsWithInvalidSizeFormat_When_ParseFromFile_Then_ThrowsInvalidGridFormatException(string filePath)
    {

        await Assert.ThrowsAsync<InvalidGridFormatException>(async () =>
            await Board.ParseFromFile(filePath));
    }
}

namespace MemoryScramble.API.Exceptions;

public class InvalidRowColumnValueException : InvalidGridFormatException
{
    public InvalidRowColumnValueException(string dimension) 
        : base($"The `{dimension}` amount must be an integer greater than zero.") { }

    public InvalidRowColumnValueException(string? message, Exception? innerException) 
        : base(message, innerException) { }
}

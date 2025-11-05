namespace MemoryScramble.API.Exceptions;

public class InvalidGridFormatException : Exception
{
    public InvalidGridFormatException() { }

    public InvalidGridFormatException(string? message) : base(message) { }

    public InvalidGridFormatException(string? message, Exception? innerException) : base(message, innerException) { }
}

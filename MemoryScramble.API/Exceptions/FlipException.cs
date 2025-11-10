namespace MemoryScramble.API.Exceptions;

public class FlipException : Exception
{
    public FlipException() { }

    public FlipException(string? message) 
        : base(message) { }

    public FlipException(string? message, Exception? innerException) 
        : base(message, innerException) { }
}

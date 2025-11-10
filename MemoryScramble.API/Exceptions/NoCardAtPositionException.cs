namespace MemoryScramble.API.Exceptions;

public class NoCardAtPositionException : FlipException
{
    public NoCardAtPositionException() 
        : base("No card at that position.") { }

    public NoCardAtPositionException(string? message) 
        : base(message) { }

    public NoCardAtPositionException(string? message, Exception? innerException) 
        : base(message, innerException) { }
}

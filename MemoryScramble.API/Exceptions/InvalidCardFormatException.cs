namespace MemoryScramble.API.Exceptions;

public class InvalidCardFormatException : InvalidGridFormatException
{
    public InvalidCardFormatException(string card) 
        : base($"Invalid card '{card}', cards must be non-empty and contain no whitespace.") { }

    public InvalidCardFormatException(string? message, Exception? innerException) 
        : base(message, innerException) { }
}

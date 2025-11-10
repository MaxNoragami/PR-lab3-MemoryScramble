namespace MemoryScramble.API.Exceptions;

public class MismatchedCardCountException : InvalidGridFormatException
{
    public MismatchedCardCountException() 
        : base("The amount of cards do not match the specified grid size.") { }

    public MismatchedCardCountException(string? message) 
        : base(message) { }

    public MismatchedCardCountException(string? message, Exception? innerException) 
        : base(message, innerException) { }
}

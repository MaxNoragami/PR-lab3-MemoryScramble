namespace MemoryScramble.API.Exceptions;

public class CardAlreadyControlledException : FlipException
{
    public CardAlreadyControlledException() 
        : base("Card is already controlled.") { }

    public CardAlreadyControlledException(string? message) 
        : base(message) { }

    public CardAlreadyControlledException(string? message, Exception? innerException) 
        : base(message, innerException) { }
}

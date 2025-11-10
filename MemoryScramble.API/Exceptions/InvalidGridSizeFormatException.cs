namespace MemoryScramble.API.Exceptions;

public class InvalidGridSizeFormatException : InvalidGridFormatException
{
    public InvalidGridSizeFormatException() 
        : base("The grid size format must match `RxC` with no spaces.") { }

    public InvalidGridSizeFormatException(string? message) 
        : base(message) { }

    public InvalidGridSizeFormatException(string? message, Exception? innerException) 
        : base(message, innerException) { }
}

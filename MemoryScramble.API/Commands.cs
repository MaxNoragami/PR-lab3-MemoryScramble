namespace PR_lab3_MemoryScramble;

public static class Commands
{
    public static Task<string> Look(Board board, string playerId)
        => Task.FromResult(board.Look(playerId));

    public static Task<string> Flip(Board board, string playerId, int row, int column)
        => Task.FromResult(board.Flip(playerId, row, column));
}

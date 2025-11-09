namespace PR_lab3_MemoryScramble.API;

public static class Commands
{
    public static Task<string> Look(Board board, string playerId)
        => Task.FromResult(board.ViewBy(playerId));

    public static async Task<string> Flip(Board board, string playerId, int row, int column)
        => await board.Flip(playerId, row, column);

    public static async Task<string> Map(Board board, string playerId, Func<string, Task<string>> f)
    {
        await board.Map(f);
        return board.ViewBy(playerId);
    }
}

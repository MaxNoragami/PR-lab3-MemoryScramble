namespace PR_lab3_MemoryScramble.API;

public static class Commands
{
    public static async Task<string> Look(Board board, string playerId)
        => await board.ViewBy(playerId);

    public static async Task<string> Flip(Board board, string playerId, int row, int column)
    {
        await board.Flip(playerId, row, column);
        return await board.ViewBy(playerId);
    }

    public static async Task<string> Map(Board board, string playerId, Func<string, Task<string>> f)
    {
        await board.Map(f);
        return await board.ViewBy(playerId);
    }

    public static async Task<string> Watch(Board board, string playerId)
        => await board.Watch(playerId);
}

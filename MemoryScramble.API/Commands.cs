namespace PR_lab3_MemoryScramble.API;

/// <summary>
/// Static commands for interacting with a Memory Scramble game board.
/// Provides a simple interface for board operations used by the web server.
/// </summary>
public static class Commands
{
    /// <summary>
    /// Returns the current state of the board as seen by the specified player.
    /// Face-up cards controlled by the player are marked as "my", other face-up cards as "up",
    /// face-down cards as "down", and empty spaces as "none".
    /// </summary>
    /// <param name="board">The game board to query</param>
    /// <param name="playerId">The ID of the player viewing the board</param>
    /// <returns>A string representation of the board state in the format:
    /// ROWSxCOLUMNS\n(SPOT\n)+ where SPOT is "none", "down", "up CARD", or "my CARD"</returns>
    public static async Task<string> Look(Board board, string playerId)
        => await board.ViewBy(playerId);

    /// <summary>
    /// Flips a card at the specified position for the given player following the Memory Scramble rules,
    /// then returns the updated board state as seen by that player.
    /// 
    /// First card: If the player has no cards flipped, this becomes their first card (rules 1-A through 1-D).
    /// Second card: If the player already has one card flipped, this becomes their second card (rules 2-A through 2-E).
    /// Cleanup: Before flipping a new first card, any previous move is cleaned up (rules 3-A and 3-B).
    /// </summary>
    /// <param name="board">The game board</param>
    /// <param name="playerId">The ID of the player making the flip</param>
    /// <param name="row">The row coordinate (0-indexed)</param>
    /// <param name="column">The column coordinate (0-indexed)</param>
    /// <returns>The updated board state after the flip</returns>
    /// <exception cref="NoCardAtPositionException">If there is no card at the specified position</exception>
    /// <exception cref="CardAlreadyControlledException">If attempting to flip a controlled card as second card</exception>
    /// <exception cref="ArgumentException">If playerId is null, empty, or whitespace</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the position is out of bounds</exception>
    public static async Task<string> Flip(Board board, string playerId, int row, int column)
    {
        await board.Flip(playerId, row, column);
        return await board.ViewBy(playerId);
    }

    /// <summary>
    /// Applies a transformation function to every card on the board, then returns the updated board state.
    /// The function f is applied to each distinct card value, and all cards with that value are replaced.
    /// Cards that are face-up or controlled remain in their current state after transformation.
    /// 
    /// The function f must be a mathematical function: calling f(x) multiple times with the same x
    /// should always produce the same result.
    /// </summary>
    /// <param name="board">The game board</param>
    /// <param name="playerId">The ID of the player requesting the transformation (for viewing result)</param>
    /// <param name="f">Transformation function that maps a card value to a new card value</param>
    /// <returns>The updated board state after the transformation</returns>
    /// <exception cref="ArgumentNullException">If f is null</exception>
    /// <exception cref="ArgumentException">If f returns null or an invalid card format</exception>
    public static async Task<string> Map(Board board, string playerId, Func<string, Task<string>> f)
    {
        await board.Map(f);
        return await board.ViewBy(playerId);
    }

    /// <summary>
    /// Registers a watcher for the given player and waits for the next board change.
    /// A change is any card turning face up/down, being removed, or changing value.
    /// Control changes (without visibility changes) do not trigger watchers.
    /// 
    /// When a change occurs, the watcher is notified with the current board state as seen by the player.
    /// This method will block until a change occurs on the board.
    /// </summary>
    /// <param name="board">The game board to watch</param>
    /// <param name="playerId">The ID of the player watching the board</param>
    /// <returns>A task that completes with the board state when a change occurs</returns>
    /// <exception cref="ArgumentException">If playerId is null, empty, or whitespace</exception>
    public static async Task<string> Watch(Board board, string playerId)
        => await board.Watch(playerId);
}

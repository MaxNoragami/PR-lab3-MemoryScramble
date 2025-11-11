using MemoryScramble.API.Exceptions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;

namespace PR_lab3_MemoryScramble.API;

/// <summary>
/// A deferred promise that can be resolved or rejected later.
/// Immutable once resolved or rejected.
/// </summary>
/// <typeparam name="T">The type of value the deferred will resolve to</typeparam>
public class Deferred<T>
{
    private TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    /// <summary>
    /// Gets the task that will complete when this deferred is resolved or rejected.
    /// </summary>
    public Task<T> Task => _tcs.Task;
    
    /// <summary>
    /// Resolves the deferred with the given value.
    /// </summary>
    /// <param name="value">The value to resolve with</param>
    public void Resolve(T value) => _tcs.TrySetResult(value);
    
    /// <summary>
    /// Rejects the deferred with the given exception.
    /// </summary>
    /// <param name="ex">The exception to reject with</param>
    public void Reject(Exception ex) => _tcs.TrySetException(ex);
}

/// <summary>
/// Mutable ADT representing a Memory Scramble game board.
/// A board consists of a grid of spaces, each potentially containing a card.
/// Multiple players can interact with the board concurrently, flipping cards to find matches.
/// </summary>
public class Board
{
    /// <summary>
    /// Represents a single cell on the board, which may contain a card.
    /// A card can be face up or face down, or the cell may be empty (card removed).
    /// </summary>
    private class Cell(string? card)
    {
        public string? Card { get; private set; } = card
            ?? throw new ArgumentNullException(nameof(card));
        public bool IsUp { get; private set; } = false;

        /// <summary>
        /// Turns this card face up if it exists.
        /// </summary>
        public void TurnUp() { if (Card != null) IsUp = true; }
        
        /// <summary>
        /// Turns this card face down if it exists.
        /// </summary>
        public void TurnDown() { if (Card != null) IsUp = false; }
        
        /// <summary>
        /// Removes the card from this cell, making it empty.
        /// </summary>
        public void Remove() { Card = null; IsUp = false; }
        
        /// <summary>
        /// Replaces the current card with a new card.
        /// </summary>
        /// <param name="newCard">The new card value (must be non-empty and non-whitespace)</param>
        /// <exception cref="ArgumentException">If newCard is null, empty, or whitespace</exception>
        public void Replace(string newCard)
        {
            if (string.IsNullOrWhiteSpace(newCard))
                throw new ArgumentException("Replacement card must be nonempty.");
            if (Card == newCard)
                return;
            Card = newCard;
        }
    }

    /// <summary>
    /// Represents the state of a player in the game, tracking which cards they have flipped.
    /// </summary>
    private class PlayerState
    {
        public (int Row, int Column)? FirstCard { get; private set; }
        public (int Row, int Column)? SecondCard { get; private set; }

        /// <summary>
        /// Sets the first card position for this player.
        /// </summary>
        /// <param name="pos">The position of the first card</param>
        public void SetFirstCard((int Row, int Column) pos)
            => FirstCard = pos;

        /// <summary>
        /// Sets the second card position for this player.
        /// </summary>
        /// <param name="pos">The position of the second card</param>
        /// <exception cref="InvalidOperationException">If FirstCard is not set</exception>
        public void SetSecondCard((int Row, int Column) pos)
            => SecondCard = (FirstCard == null)
                ? throw new InvalidOperationException("Cannot set SecondCard before FirstCard.")
                : pos;

        /// <summary>
        /// Clears both card positions for this player.
        /// </summary>
        public void ClearCards() { FirstCard = null; SecondCard = null; }
    }

    private readonly Cell[,] _grid;
    private readonly Dictionary<(int Row, int Column), string> _controlledBy;
    private readonly Dictionary<string, PlayerState> _players;
    private readonly Dictionary<(int Row, int Column), HashSet<Deferred<object?>>> _holds;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<Deferred<string>, string> _watchers = new();
    private readonly string[] _initialCards; // Store initial state for reset

    // Rep invariant:
    //   - _grid is not null, with dimensions Rows x Columns (both > 0)
    //   - Each Cell in _grid is non-null
    //   - For each cell: if Card is null, then IsUp is false and cell position is not in _controlledBy
    //   - For each cell with non-null Card: Card matches CardRegex (^\S+$)
    //   - For each (pos, playerId) in _controlledBy:
    //       * pos is valid (0 <= pos.Row < Rows && 0 <= pos.Column < Columns)
    //       * _grid[pos].Card is non-null and _grid[pos].IsUp is true
    //       * playerId exists as a key in _players
    //       * pos appears in _players[playerId].FirstCard or SecondCard
    //   - For each (playerId, state) in _players:
    //       * playerId is non-empty string
    //       * If state.SecondCard is set, state.FirstCard must also be set
    //       * If state.FirstCard is set and state.SecondCard is null, 
    //         FirstCard must be controlled by playerId in _controlledBy
    //   - _lock, _holds, _watchers, _players, _controlledBy are all non-null
    //
    // Abstraction function:
    //   AF(_grid, _controlledBy, _players, _holds, _watchers) = 
    //     A Memory Scramble game board where:
    //       - The board has Rows x Columns positions arranged in a grid
    //       - Each position (r,c) where 0 <= r < Rows and 0 <= c < Columns either:
    //           * Contains a face-down card with value _grid[r,c].Card
    //           * Contains a face-up card with value _grid[r,c].Card, 
    //             possibly controlled by a player
    //           * Is empty (no card) if _grid[r,c].Card is null
    //       - Players (identified by their string IDs) may have:
    //           * No cards flipped (not in _players, or both FirstCard and SecondCard are null)
    //           * One card flipped and controlled (FirstCard set, SecondCard null)
    //           * Two cards flipped (both FirstCard and SecondCard set), either:
    //             - Matching: player controls both cards
    //             - Non-matching: player controls neither card
    //       - Some players may be waiting to control specific cards (_holds)
    //       - Some players may be watching for board changes (_watchers)
    //
    // Safety from rep exposure:
    //   - All fields are private and readonly where applicable
    //   - _grid is a 2D array of Cell objects; Cell is a private nested class never exposed
    //   - ViewBy() and Watch() return strings (immutable) representing board state
    //   - ParseFromFile() and ParseFromLines() create new Board instances, not exposing internals
    //   - PlayerState is a private nested class, never exposed to clients
    //   - All dictionaries (_controlledBy, _players, _holds, _watchers) are private
    //     and never returned directly; their contents are only accessed internally
    //   - Public methods (Flip, Map, ViewBy, Watch) only accept/return immutable types
    //     (string, int, Task<string>, Func<string, Task<string>>)
    //   - The only way to observe board state is through ViewBy/Watch which return strings
    //   - The only way to mutate board state is through Flip and Map operations,
    //     which are synchronized via _lock to maintain invariants
    //   - Deferred<T> objects in _holds and _watchers are used internally only for
    //     coordination; they are never exposed to clients

    private static readonly Regex CardRegex = new(@"^\S+$", RegexOptions.Compiled);
    private static readonly Regex GridHeaderRegex = new(@"^[0-9]+x[0-9]+$", RegexOptions.Compiled);

    /// <summary>
    /// Gets the number of rows in the board.
    /// </summary>
    public int Rows { get; init; }
    
    /// <summary>
    /// Gets the number of columns in the board.
    /// </summary>
    public int Columns { get; init; }

    /// <summary>
    /// Creates a new board with the specified dimensions and grid.
    /// </summary>
    /// <param name="rows">Number of rows (must be positive)</param>
    /// <param name="columns">Number of columns (must be positive)</param>
    /// <param name="grid">The grid of cells representing the board</param>
    /// <param name="initialCards">The initial card values for reset functionality</param>
    private Board(int rows, int columns, Cell[,] grid, string[] initialCards)
    {
        Rows = rows;
        Columns = columns;
        _grid = grid;
        _controlledBy = new();
        _players = new();
        _holds = new();
        _initialCards = initialCards;
        CheckRep();
    }

    /// <summary>
    /// Parses a board from a file.
    /// </summary>
    /// <param name="fileName">The name of the file containing the board data (relative to program directory)</param>
    /// <returns>A new Board parsed from the file</returns>
    /// <exception cref="FileNotFoundException">If the file cannot be found</exception>
    /// <exception cref="InvalidGridSizeFormatException">If the header doesn't match ROWSxCOLUMNS format</exception>
    /// <exception cref="InvalidRowColumnValueException">If row or column value is invalid</exception>
    /// <exception cref="MismatchedCardCountException">If the number of cards doesn't match rows × columns</exception>
    /// <exception cref="InvalidCardFormatException">If any card contains whitespace or is empty</exception>
    public static async Task<Board> ParseFromFile(string fileName)
    {
        var dataLines = await ReadAllLinesAsync(fileName);
        return ParseFromLines(dataLines);
    }
    
    /// <summary>
    /// Parses a board from an array of text lines.
    /// </summary>
    /// <param name="dataLines">Array of lines where first line is "ROWSxCOLUMNS" and remaining lines are card values</param>
    /// <returns>A new Board parsed from the lines</returns>
    /// <exception cref="InvalidGridSizeFormatException">If the header doesn't match ROWSxCOLUMNS format</exception>
    /// <exception cref="InvalidRowColumnValueException">If row or column value is invalid</exception>
    /// <exception cref="MismatchedCardCountException">If the number of cards doesn't match rows × columns</exception>
    /// <exception cref="InvalidCardFormatException">If any card contains whitespace or is empty</exception>
    public static Board ParseFromLines(string[] dataLines)
    {
        var header = dataLines[0];

        if (!GridHeaderRegex.IsMatch(header))
            throw new InvalidGridSizeFormatException();

        var parts = header.Split('x');
        if (!int.TryParse(parts[0], out int rows) || rows <= 0)
            throw new InvalidRowColumnValueException("rows");

        if (!int.TryParse(parts[1], out int columns) || columns <= 0)
            throw new InvalidRowColumnValueException("columns");

        if (rows * columns != dataLines.Length - 1)
            throw new MismatchedCardCountException();

        var grid = new Cell[rows, columns];

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < columns; j++)
            {
                var card = dataLines[(i * columns) + j + 1];

                if (!CardRegex.IsMatch(card))
                    throw new InvalidCardFormatException(card);

                grid[i, j] = new Cell(card);
            }

        // Store initial cards for reset functionality (skip header line)
        var initialCards = new string[dataLines.Length - 1];
        Array.Copy(dataLines, 1, initialCards, 0, initialCards.Length);

        return new Board(rows, columns, grid, initialCards);
    }

    /// <summary>
    /// Returns the current state of the board as seen by the specified player.
    /// Face-up cards controlled by the player are marked as "my", other face-up cards as "up",
    /// face-down cards as "down", and empty spaces as "none".
    /// </summary>
    /// <param name="playerId">The ID of the player viewing the board (must be non-empty)</param>
    /// <returns>A string representation of the board state in the format:
    /// ROWSxCOLUMNS\n(SPOT\n)+ where SPOT is "none", "down", "up CARD", or "my CARD"</returns>
    /// <exception cref="ArgumentException">If playerId is null, empty, or whitespace</exception>
    public async Task<string> ViewBy(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID cannot be null or empty.", nameof(playerId));

        await _lock.WaitAsync();
        try
        {
            CheckRep();

            var boardState = new StringBuilder();
            boardState.AppendLine($"{Rows}x{Columns}");

            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Columns; j++)
                {
                    var cell = _grid[i, j];

                    var spot = cell.Card switch
                    {
                        null => "none",
                        _ when !cell.IsUp => "down",
                        _ when IsControlledBy((i, j), playerId) => $"my {cell.Card}",
                        _ => $"up {cell.Card}"
                    };

                    boardState.AppendLine(spot);
                }

            return boardState.ToString();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Flips a card at the specified position for the given player, following the Memory Scramble game rules.
    /// 
    /// First card rules (1-A through 1-D):
    /// - 1-A: If no card exists at the position, throws NoCardAtPositionException
    /// - 1-B: If card is face down, turns it face up and player controls it
    /// - 1-C: If card is face up but not controlled, player takes control
    /// - 1-D: If card is controlled by another player, waits until control is available
    /// 
    /// Second card rules (2-A through 2-E):
    /// - 2-A: If no card exists, fails and player relinquishes control of first card
    /// - 2-B: If card is already controlled, fails and player relinquishes control of first card
    /// - 2-C: If card is face down, turns it face up
    /// - 2-D: If cards match, player keeps control of both
    /// - 2-E: If cards don't match, player relinquishes control of both
    /// 
    /// Cleanup rules (3-A and 3-B):
    /// - 3-A: If previous cards matched, they are removed from the board
    /// - 3-B: If previous cards didn't match, they turn face down (if not controlled)
    /// </summary>
    /// <param name="playerId">The ID of the player making the flip (must be non-empty)</param>
    /// <param name="row">The row coordinate (0-indexed)</param>
    /// <param name="column">The column coordinate (0-indexed)</param>
    /// <returns>A task that completes when the flip operation finishes</returns>
    /// <exception cref="ArgumentException">If playerId is null, empty, or whitespace</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the position is out of bounds</exception>
    /// <exception cref="NoCardAtPositionException">If there is no card at the specified position</exception>
    /// <exception cref="CardAlreadyControlledException">If attempting to flip a controlled card as second card</exception>
    public async Task Flip(string playerId, int row, int column)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID cannot be null or empty.", nameof(playerId));

        if (!IsValidPosition(row, column))
            throw new ArgumentOutOfRangeException($"Position ({row},{column}) is out of bounds.");

        var toResolve = new List<Deferred<object?>>();
        var visibleChanged = false;
        
        await _lock.WaitAsync();
        try
        {
            if (!_players.ContainsKey(playerId))
                _players[playerId] = new PlayerState();

            var playerState = _players[playerId];

            if (playerState.SecondCard != null)
                visibleChanged |= CleanupPreviousMove(playerId, toResolve, (row, column));

            if (playerState.FirstCard == null)
                visibleChanged |= await FlipFirstCardAsync(playerId, row, column);
            else
                visibleChanged |= FlipSecondCard(playerId, row, column, toResolve);

            CheckRep();
        }
        finally
        {
            _lock.Release();
            foreach (var hold in toResolve)
                hold.Resolve(null);
            
            if (visibleChanged)
                await NotifyWatchersAsync();
        }
    }

    /// <summary>
    /// Applies a transformation function to every card on the board.
    /// The function f is applied to each distinct card value, and all cards with that value
    /// are replaced with the result. The transformation is applied atomically per card value.
    /// Cards that are face-up or controlled remain in their current state after transformation.
    /// 
    /// The function f must be a mathematical function: calling f(x) multiple times with the same x
    /// should always produce the same result. The function may be asynchronous.
    /// 
    /// While Map is running, other operations may interleave, but the board will remain consistent:
    /// if two cards match at the start of Map, no player will observe a state where they don't match
    /// during the transformation.
    /// </summary>
    /// <param name="f">Transformation function that maps a card value to a new card value</param>
    /// <returns>A task that completes when all transformations are applied</returns>
    /// <exception cref="ArgumentNullException">If f is null</exception>
    /// <exception cref="ArgumentException">If f returns null or an invalid card format</exception>
    public async Task Map(Func<string, Task<string>> f)
    {
        if (f is null)
            throw new ArgumentNullException(nameof(f));

        // Snapshot groups of positions by their ORIGINAL card value
        Dictionary<string, List<(int Row, int Column)>> groups;
        await _lock.WaitAsync();
        try
        {
            groups = new();
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Columns; j++)
                {
                    var cell = _grid[i, j];
                    if (cell.Card is null)
                        continue;
                    
                    var key = cell.Card;
                    if (!groups.TryGetValue(key, out var list))
                    {
                        list = new List<(int, int)>();
                        groups[key] = list;
                    }
                    list.Add((i, j));
                }
        }
        finally
        {
            _lock.Release();
        }

        // For each distinct original value, transform once async, then atomically replace all its copies
        var tasks = new List<Task>(capacity: groups.Count);

        foreach (var (original, positions) in groups)
        {
            tasks.Add(Task.Run(async () =>
            {
                // Compute replacement WITHOUT holding the board lock
                var replacement = await f(original).ConfigureAwait(false);
                if (replacement is null)
                    throw new ArgumentException("Transformer returned null.");
                if (!CardRegex.IsMatch(replacement))
                    throw new ArgumentException($"Transformer produced invalid card '{replacement}'.");

                if (replacement == original)
                    return;

                var notify = false;

                // Apply to all cells that STILL have the original value, atomically as one step, then after releasing the lock notify watchers
                await _lock.WaitAsync().ConfigureAwait(false);
                try
                {
                    foreach (var (r, c) in positions)
                    {
                        var cell = _grid[r, c];
                        if (cell.Card == original)
                        {
                            cell.Replace(replacement);
                            notify = true;
                        }
                    }
                    CheckRep();
                }
                finally
                {
                    _lock.Release();
                }

                if (notify)
                    await NotifyWatchersAsync().ConfigureAwait(false);
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers a watcher for the given player and waits for the next board change.
    /// A change is any card turning face up/down, being removed, or changing value.
    /// Control changes (without visibility changes) do not trigger watchers.
    /// 
    /// When a change occurs, the watcher is notified with the current board state as seen by the player.
    /// Multiple watchers can be registered and will all be notified of the next change.
    /// </summary>
    /// <param name="playerId">The ID of the player watching the board (must be non-empty)</param>
    /// <returns>A task that completes with the board state when a change occurs</returns>
    /// <exception cref="ArgumentException">If playerId is null, empty, or whitespace</exception>
    public async Task<string> Watch(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID cannot be null or empty.", nameof(playerId));

        var watcher = new Deferred<string>();

        await _lock.WaitAsync();
        try
        {
            CheckRep();
            _watchers[watcher] = playerId;
        }
        finally
        {
            _lock.Release();
        }

        return await watcher.Task;
    }
    
    /// <summary>
    /// Resets the board to its initial state.
    /// All cards are restored to their original positions face-down.
    /// All players lose control of their cards and their game state is cleared.
    /// All waiting operations are rejected with an OperationCanceledException.
    /// All watchers are notified of the board change.
    /// </summary>
    /// <returns>A task that completes when the reset is finished</returns>
    public async Task Reset()
    {        
        await _lock.WaitAsync();
        try
        {
            // Restore all cards to initial state, face down
            int cardIndex = 0;
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Columns; j++)
                {
                    var cell = _grid[i, j];
                    cell.Replace(_initialCards[cardIndex++]);
                    cell.TurnDown();
                }

            // Clear all control
            _controlledBy.Clear();

            // Clear all player states
            _players.Clear();

            // Reject all waiting operations
            foreach (var holdSet in _holds.Values)
                foreach (var hold in holdSet)
                    hold.Reject(new OperationCanceledException("Board was reset"));

            _holds.Clear();

            CheckRep();
        }
        finally
        {
            _lock.Release();
        }

        // Notify all watchers about the reset
        await NotifyWatchersAsync();
    }
    
    /// <summary>
    /// Notifies all registered watchers of a board change by resolving their promises
    /// with the current board state as seen by each watcher's player.
    /// </summary>
    /// <returns>A task that completes when all watchers have been notified</returns>
    private async Task NotifyWatchersAsync()
    {
        List<(Deferred<string> w, string pid)> snapshot;

        await _lock.WaitAsync();
        try
        {
            if (_watchers.Count == 0)
                return;
            snapshot = _watchers.Select(kv => (kv.Key, kv.Value)).ToList();
            _watchers.Clear();
        }
        finally
        {
            _lock.Release();
        }

        foreach (var (watcher, playerId) in snapshot)
        {
            var view = await ViewBy(playerId);
            watcher.Resolve(view);
        }
    }

    /// <summary>
    /// Returns a string representation of the board showing all cards and their states.
    /// Used for debugging. Format: ROWSxCOLUMNS followed by grid where:
    /// - "." represents an empty space
    /// - "?" represents a face-down card
    /// - "[CARD]" represents a face-up card
    /// </summary>
    /// <returns>A debug-friendly string representation of the board</returns>
    public override string ToString()
    {
        CheckRep();

        var boardState = new StringBuilder();
        boardState.AppendLine($"{Rows}x{Columns}");
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                var cell = _grid[i, j];
                boardState.Append(cell.Card == null ? ". " : cell.IsUp ? $"[{cell.Card}] " : "? ");
            }
            boardState.AppendLine();
        }

        return boardState.ToString();
    }

    /// <summary>
    /// Checks the representation invariant of the Board.
    /// Asserts that all invariants documented in the RI comment are maintained.
    /// Called at the end of every operation that creates or mutates the board.
    /// </summary>
    private void CheckRep()
    {
        Debug.Assert(_grid != null);
        Debug.Assert(Rows > 0 && Columns > 0);
        Debug.Assert(_grid.GetLength(0) == Rows);
        Debug.Assert(_grid.GetLength(1) == Columns);

        for (int i = 0; i < Rows; i++)
            for (int j = 0; j < Columns; j++)
            {
                var cell = _grid[i, j];
                Debug.Assert(cell != null, $"Cell ({i},{j}) must not be null.");

                if (cell.Card == null)
                {
                    Debug.Assert(!cell.IsUp, "A removed card cannot be face up.");
                    Debug.Assert(!_controlledBy.ContainsKey((i, j)), "Removed card cannot be controlled.");
                }
                else
                    Debug.Assert(CardRegex.IsMatch(cell.Card),
                        $"Invalid card string at ({i},{j}): '{cell.Card}'");
            }

        foreach (var (pos, pid) in _controlledBy)
        {
            var (row, col) = pos;

            Debug.Assert(IsValidPosition(row, col), "Controlled card out of bounds.");

            var cell = _grid[row, col];
            Debug.Assert(cell.Card != null, "Controlled card must exist.");
            Debug.Assert(cell.IsUp, "Controlled card must be face up.");
            Debug.Assert(!string.IsNullOrEmpty(pid), "ControlledBy contains empty player id.");
            Debug.Assert(_players.ContainsKey(pid), $"_controlledBy references unknown player '{pid}'.");

            var pstate = _players[pid];
            bool inPlayerState = pstate.FirstCard == pos || pstate.SecondCard == pos;
            Debug.Assert(inPlayerState,
                $"_controlledBy position ({row},{col}) not present in player {pid}'s state.");
        }

        foreach (var (pid, state) in _players)
        {
            Debug.Assert(!(state.FirstCard == null && state.SecondCard != null),
                $"Player {pid} has SecondCard but no FirstCard.");

            if (state.FirstCard is { } fp)
            {
                Debug.Assert(IsValidPosition(fp.Row, fp.Column),
                    $"Player {pid}'s FirstCard out of bounds.");

                if (state.SecondCard == null)
                    Debug.Assert(IsControlledBy(fp, pid),
                        $"Player {pid}'s FirstCard should be controlled until second flip.");
            }

            if (state.SecondCard is { } sp)
            {
                Debug.Assert(IsValidPosition(sp.Row, sp.Column),
                    $"Player {pid}'s SecondCard out of bounds.");

                if (IsControlledBy(sp, pid))
                    Debug.Assert(state.FirstCard != null && IsControlledBy(state.FirstCard.Value, pid),
                        $"Player {pid} controls SecondCard but not FirstCard.");
            }
        }
    }

    /// <summary>
    /// Reads all lines from a file asynchronously.
    /// The file path is resolved relative to the program's base directory.
    /// </summary>
    /// <param name="fileName">The relative file name</param>
    /// <returns>An array of lines read from the file</returns>
    /// <exception cref="FileNotFoundException">If the file cannot be found</exception>
    private static async Task<string[]> ReadAllLinesAsync(string fileName)
    {
        var dir = AppContext.BaseDirectory;

        var filePath = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", fileName));
        if (!Path.Exists(filePath))
            throw new FileNotFoundException($"Could not find file '{fileName}' at {filePath}.");

        return await File.ReadAllLinesAsync(filePath);
    }

    /// <summary>
    /// Checks if a position is within the valid bounds of the board.
    /// </summary>
    /// <param name="row">The row coordinate</param>
    /// <param name="column">The column coordinate</param>
    /// <returns>True if the position is valid, false otherwise</returns>
    private bool IsValidPosition(int row, int column)
        => row >= 0 && row < Rows && column >= 0 && column < Columns;

    /// <summary>
    /// Checks if a position is currently controlled by any player.
    /// </summary>
    /// <param name="pos">The position to check</param>
    /// <returns>True if the position is controlled, false otherwise</returns>
    private bool IsControlled((int Row, int Column) pos)
        => _controlledBy.ContainsKey(pos);

    /// <summary>
    /// Checks if a position is controlled by a specific player.
    /// </summary>
    /// <param name="pos">The position to check</param>
    /// <param name="playerId">The player ID to check</param>
    /// <returns>True if the position is controlled by the player, false otherwise</returns>
    private bool IsControlledBy((int Row, int Column) pos, string playerId)
        => _controlledBy.TryGetValue(pos, out var controller) && controller == playerId;

    /// <summary>
    /// Gives control of a position to a player.
    /// </summary>
    /// <param name="playerId">The player ID to give control to</param>
    /// <param name="pos">The position to control</param>
    private void TakeControl(string playerId, (int Row, int Column) pos)
        => _controlledBy[pos] = playerId;

    /// <summary>
    /// Turns a card face up if it is currently face down.
    /// </summary>
    /// <param name="cell">The cell containing the card</param>
    /// <returns>True if the card state changed (was face down), false otherwise</returns>
    private bool TurnUpIfNeeded(Cell cell)
    {
        if (!cell.IsUp && cell.Card != null)
        {
            cell.TurnUp();
            return true; 
        }
        return false;
    }

    /// <summary>
    /// Turns a card face down if it is face up and not controlled by any player.
    /// Implements rule 3-B: cards turn face down only if they exist, are face up,
    /// and are not currently controlled.
    /// </summary>
    /// <param name="pos">The position of the card</param>
    /// <returns>True if the card state changed (was turned down), false otherwise</returns>
    private bool TurnDownIfPossible((int Row, int Column) pos)
    {
        var cell = _grid[pos.Row, pos.Column];

        // Rule 3-B: Turn face down if:
        // - Card still exists
        // - Card is face up
        // - Card is not controlled by another player
        if (cell.Card != null && cell.IsUp && !IsControlled(pos))
        {
            cell.TurnDown();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a card from the board if it exists at the given position.
    /// </summary>
    /// <param name="pos">The position of the card to remove</param>
    /// <returns>True if a card was removed, false if position was already empty</returns>
    private bool RemoveIfPresent((int Row, int Column) pos)
    {
        var cell = _grid[pos.Row, pos.Column];
        if (cell.Card != null)
        {
            cell.Remove();
            return true; 
        }
        return false;
    }
    
    /// <summary>
    /// Relinquishes control of a position and resolves any waiting promises for that position.
    /// Implements the control handoff mechanism: when a player gives up control, any players
    /// waiting for that position are notified (their promises are added to toResolve).
    /// </summary>
    /// <param name="pos">The position to relinquish control of</param>
    /// <param name="toResolve">List of promises to resolve outside the lock</param>
    private void GiveUpControl((int Row, int Column) pos, List<Deferred<object?>> toResolve)
    {
        _controlledBy.Remove(pos);

        if (_holds.TryGetValue(pos, out var waiters) && waiters.Count > 0)
        {
            toResolve.AddRange(waiters);
            waiters.Clear();
            _holds.Remove(pos);
        }
    }

    /// <summary>
    /// Handles flipping the first card for a player following rules 1-A through 1-D.
    /// - 1-A: Throws if no card exists
    /// - 1-B: Turns card face up if needed
    /// - 1-C: Player takes control
    /// - 1-D: Waits if card is controlled by another player
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <param name="row">The row coordinate</param>
    /// <param name="column">The column coordinate</param>
    /// <returns>True if the board state changed visibly, false otherwise</returns>
    /// <exception cref="NoCardAtPositionException">If no card exists at the position</exception>
    private async Task<bool> FlipFirstCardAsync(string playerId, int row, int column)
    {
        var cell = _grid[row, column];
        var pos = (row, column);

        // Rule 1-A: No card there
        if (cell.Card == null)
            throw new NoCardAtPositionException();

        // Rule 1-D: Card is controlled by another dude
        while (IsControlled(pos) && !IsControlledBy(pos, playerId))
        {
            var hold = new Deferred<object?>();

            if (!_holds.ContainsKey(pos))
                _holds[pos] = new HashSet<Deferred<object?>>();

            _holds[pos].Add(hold);

            _lock.Release();
            try
            {
                await hold.Task;
            }
            finally
            {
                await _lock.WaitAsync();

                if (_holds.TryGetValue(pos, out var waiters))
                {
                    waiters.Remove(hold);
                    if (waiters.Count == 0)
                        _holds.Remove(pos);
                }
            }

            if (cell.Card == null)
                throw new NoCardAtPositionException();
        }

        // Rule 1-B: Turn the card face up
        var changed = TurnUpIfNeeded(cell);

        // Rule 1-C: Player takes control, as card is already face up, due to 1-B and is not controlled by another as ensured in 1-D
        TakeControl(playerId, pos);
        _players[playerId].SetFirstCard(pos);
        return changed;
    }

    /// <summary>
    /// Handles flipping the second card for a player following rules 2-A through 2-E.
    /// - 2-A: Throws if no card exists (and relinquishes first card control)
    /// - 2-B: Throws if card is already controlled (and relinquishes first card control)
    /// - 2-C: Turns card face up if needed
    /// - 2-D: Player keeps control of both if they match
    /// - 2-E: Player relinquishes control of both if they don't match
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <param name="row">The row coordinate</param>
    /// <param name="column">The column coordinate</param>
    /// <param name="toResolve">List of promises to resolve outside the lock</param>
    /// <returns>True if the board state changed visibly, false otherwise</returns>
    /// <exception cref="NoCardAtPositionException">If no card exists at the position</exception>
    /// <exception cref="CardAlreadyControlledException">If the card is already controlled</exception>
    private bool FlipSecondCard(string playerId, int row, int column, List<Deferred<object?>> toResolve)
    {
        var cell = _grid[row, column];
        var pos = (row, column);
        var playerState = _players[playerId];
        var firstPos = playerState.FirstCard!.Value;
        var firstCell = _grid[firstPos.Row, firstPos.Column];

        // Rule 2-A: No card there
        if (cell.Card == null)
        {
            GiveUpControl(firstPos, toResolve);
            playerState.SetSecondCard(firstPos);
            throw new NoCardAtPositionException();
        }

        // Rule 2-B: No waiting on second card, cannot select an already controlled card
        if (IsControlled(pos))
        {
            GiveUpControl(firstPos, toResolve);
            playerState.SetSecondCard(firstPos);
            throw new CardAlreadyControlledException();
        }   

        // Rule 2-C: Turn cards facing down to facing up
        var changed = TurnUpIfNeeded(cell);

        if (firstCell.Card != cell.Card)
        {
            // Rule 2-E: No match => give up control of both cards, they remain face up
            GiveUpControl(firstPos, toResolve);
            playerState.SetSecondCard(pos);
        }
        else
        {
            // Rule 2-D: Match => keep control of both cards, they remain face up
            TakeControl(playerId, pos);
            playerState.SetSecondCard(pos);
        }

        return changed;
    }

    /// <summary>
    /// Cleans up a player's previous move following rules 3-A and 3-B.
    /// - 3-A: If cards matched (player controls both), remove them from the board
    /// - 3-B: If cards didn't match, turn them face down (if not controlled by another player)
    /// 
    /// This is called before a player flips a new first card, finishing their previous play.
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <param name="toResolve">List of promises to resolve outside the lock</param>
    /// <param name="excludePos">Optional position to exclude from turning face down (the new flip position)</param>
    /// <returns>True if the board state changed visibly, false otherwise</returns>
    private bool CleanupPreviousMove(string playerId, List<Deferred<object?>> toResolve, (int Row, int Column)? excludePos = null)
    {
        var playerState = _players[playerId];
        var firstPos = playerState.FirstCard!.Value;
        var secondPos = playerState.SecondCard!.Value;

        var changed = false;

        if (firstPos == secondPos)
        {
            // Rule 3-B: Only first card to turn down, second flip failed
            if (firstPos != excludePos)
                changed |= TurnDownIfPossible(firstPos);
            playerState.ClearCards();
            return changed;
        }

        bool matched = IsControlledBy(firstPos, playerId)
            && IsControlledBy(secondPos, playerId);

        if (matched)
        {
            changed |= RemoveIfPresent(firstPos);
            GiveUpControl(firstPos, toResolve);

            changed |= RemoveIfPresent(secondPos);
            GiveUpControl(secondPos, toResolve);
        }
        else
        {
            // Rule 3-B: Turn down non-matching cards if conditions are met
            if (firstPos != excludePos)
                changed |= TurnDownIfPossible(firstPos);
            if (secondPos != excludePos)
                changed |= TurnDownIfPossible(secondPos);
        }

        playerState.ClearCards();
        return changed;
    }
}

using MemoryScramble.API.Exceptions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;

namespace PR_lab3_MemoryScramble.API;

public class Deferred<T>
{
    private TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<T> Task => _tcs.Task;
    public void Resolve(T value) => _tcs.TrySetResult(value);
    public void Reject(Exception ex) => _tcs.TrySetException(ex);
}

public class Board
{
    private class Cell(string? card)
    {
        public string? Card { get; private set; } = card
            ?? throw new ArgumentNullException(nameof(card));
        public bool IsUp { get; private set; } = false;

        public void TurnUp() { if (Card != null) IsUp = true; }
        public void TurnDown() { if (Card != null) IsUp = false; }
        public void Remove() { Card = null; IsUp = false; }
        public void Replace(string newCard)
        {
            if (string.IsNullOrWhiteSpace(newCard))
                throw new ArgumentException("Replacement card must be nonempty.");
            if (Card == newCard)
                return;
            Card = newCard;
        }
    }

    private class PlayerState
    {
        public (int Row, int Column)? FirstCard { get; private set; }
        public (int Row, int Column)? SecondCard { get; private set; }

        public void SetFirstCard((int Row, int Column) pos)
            => FirstCard = pos;

        public void SetSecondCard((int Row, int Column) pos)
            => SecondCard = (FirstCard == null)
                ? throw new InvalidOperationException("Cannot set SecondCard before FirstCard.")
                : pos;

        public void ClearCards() { FirstCard = null; SecondCard = null; }
    }

    private readonly Cell[,] _grid;
    private readonly Dictionary<(int Row, int Column), string> _controlledBy;
    private readonly Dictionary<string, PlayerState> _players;


    private readonly Dictionary<(int Row, int Column), HashSet<Deferred<object?>>> _holds;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly Dictionary<Deferred<string>, string> _watchers = new();


    private static readonly Regex CardRegex = new(@"^\S+$", RegexOptions.Compiled);
    private static readonly Regex GridHeaderRegex = new(@"^[0-9]+x[0-9]+$", RegexOptions.Compiled);


    public int Rows { get; init; }
    public int Columns { get; init; }


    private Board(int rows, int columns, Cell[,] grid)
    {
        Rows = rows;
        Columns = columns;
        _grid = grid;
        _controlledBy = new();
        _players = new();
        _holds = new();
        CheckRep();
    }

    public static async Task<Board> ParseFromFile(string fileName)
    {
        var dataLines = await ReadAllLinesAsync(fileName);
        return ParseFromLines(dataLines);
    }
    
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

        return new Board(rows, columns, grid);
    }

    public string ViewBy(string playerId)
    {
        CheckRep();

        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID cannot be null or empty.", nameof(playerId));

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

    public async Task<string> Watch(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID cannot be null or empty.", nameof(playerId));

        var watcher = new Deferred<string>();

        await _lock.WaitAsync();
        try
        {
            _watchers[watcher] = playerId;
        }
        finally
        {
            _lock.Release();
        }

        return await watcher.Task;
    }
    
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
            watcher.Resolve(ViewBy(playerId));
    }

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

    private static async Task<string[]> ReadAllLinesAsync(string fileName)
    {
        var dir = AppContext.BaseDirectory;

        var filePath = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", fileName));
        if (!Path.Exists(filePath))
            throw new FileNotFoundException($"Could not find file '{fileName}' at {filePath}.");

        return await File.ReadAllLinesAsync(filePath);
    }

    private bool IsValidPosition(int row, int column)
        => row >= 0 && row < Rows && column >= 0 && column < Columns;

    private bool IsControlled((int Row, int Column) pos)
        => _controlledBy.ContainsKey(pos);

    private bool IsControlledBy((int Row, int Column) pos, string playerId)
        => _controlledBy.TryGetValue(pos, out var controller) && controller == playerId;

    private void TakeControl(string playerId, (int Row, int Column) pos)
        => _controlledBy[pos] = playerId;

    private bool TurnUpIfNeeded(Cell cell)
    {
        if (!cell.IsUp && cell.Card != null)
        {
            cell.TurnUp();
            return true; 
        }
        return false;
    }

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

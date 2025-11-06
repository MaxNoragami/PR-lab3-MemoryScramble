using MemoryScramble.API.Exceptions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;

namespace PR_lab3_MemoryScramble.API;

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
        CheckRep();
    }

    public static async Task<Board> ParseFromFile(string fileName)
    {
        var dataLines = await ReadAllLinesAsync(fileName);

        var header = dataLines[0];

        if (!GridHeaderRegex.IsMatch(header))
            throw new InvalidGridFormatException("The grid size format must match `RxC` with no spaces.");

        var parts = header.Split('x');
        if (!int.TryParse(parts[0], out int rows) || rows <= 0)
            throw new InvalidGridFormatException("The `rows` amount must be an integer greater than zero.");

        if (!int.TryParse(parts[1], out int columns) || columns <= 0)
            throw new InvalidGridFormatException("The `columns` amount must be an integer greater than zero.");

        if (rows * columns != dataLines.Length - 1)
            throw new InvalidGridFormatException("The amount of cards do not match the specified grid size.");

        var grid = new Cell[rows, columns];

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < columns; j++)
            {
                var card = dataLines[(i * columns) + j + 1];
                
                if (!CardRegex.IsMatch(card))
                    throw new InvalidGridFormatException($"Invalid card '{card}', cards must be non-empty and contain no whitespace.");

                grid[i, j] = new Cell(card);
            }          

        return new Board(rows, columns, grid);
    }

    public string ViewBy(string playerId)
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

    public string Flip(string playerId, int row, int column)
    {
        CheckRep();

        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID cannot be null or empty.", nameof(playerId));

        if (!IsValidPosition(row, column))
            throw new ArgumentOutOfRangeException($"Position ({row},{column}) is out of bounds.");

        if (!_players.ContainsKey(playerId))
            _players[playerId] = new PlayerState();

        var playerState = _players[playerId];

        if (playerState.SecondCard != null)
            CleanupPreviousMove(playerId);

        if (playerState.FirstCard == null)
            FlipFirstCard(playerId, row, column);
        else
            FlipSecondCard(playerId, row, column);

        CheckRep();
        return ViewBy(playerId);
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

            if (state.FirstCard != null)
                CheckPlayerCard(pid, state.FirstCard.Value, isFirst: true);

            if (state.SecondCard != null)
                CheckPlayerCard(pid, state.SecondCard.Value, isFirst: false);
        }
    }

    private void CheckPlayerCard(string playerId, (int Row, int Column) pos, bool isFirst)
    {
        var cardName = isFirst ? "FirstCard" : "SecondCard";
        var (r, c) = pos;

        Debug.Assert(IsValidPosition(r, c), $"Player {playerId}'s {cardName} out of bounds.");

        var cell = _grid[r, c];
        Debug.Assert(cell.Card != null && cell.IsUp,
            $"Player {playerId}'s {cardName} must be an existent, face-up card.");

        if (isFirst)
            if (_players[playerId].SecondCard == null)
                Debug.Assert(IsControlledBy(pos, playerId),
                    $"Player {playerId}'s FirstCard must be controlled by that player.");
        else
            if (IsControlledBy(pos, playerId))
            {
                var firstPos = _players[playerId].FirstCard!.Value;
                var firstCell = _grid[firstPos.Row, firstPos.Column];
                Debug.Assert(firstCell.Card == cell.Card,
                    "If player controls SecondCard, cards must match.");
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

    private void GiveUpControl((int Row, int Column) pos)
        => _controlledBy.Remove(pos);

    private void FlipFirstCard(string playerId, int row, int column)
    {
        var cell = _grid[row, column];
        var pos = (row, column);

        // Rule 1-A: No card there
        if (cell.Card == null)
            throw new FlipException("No card at that position.");

        // Rule 1-D: Card is controlled by another dude
        if (IsControlled(pos) && !IsControlledBy(pos, playerId))
            throw new FlipException("Card is controlled by another player.");

        // Rule 1-B: Turn the card face up
        if (!cell.IsUp)
            cell.TurnUp();

        // Rule 1-C: Player takes control, as card is already face up, due to 1-B and is not controlled by another as ensured in 1-D
        TakeControl(playerId, pos);
        _players[playerId].SetFirstCard(pos);
    }

    private void FlipSecondCard(string playerId, int row, int column)
    {
        var cell = _grid[row, column];
        var pos = (row, column);
        var playerState = _players[playerId];
        var firstPos = playerState.FirstCard!.Value;
        var firstCell = _grid[firstPos.Row, firstPos.Column];

        // Rule 2-A: No card there
        if (cell.Card == null)
        {
            GiveUpControl(firstPos);
            playerState.ClearCards();
            throw new FlipException("No card at that position.");
        }
        
        // Rule 2-B: No waiting on second card, cannot select an already controlled card
        if (IsControlled(pos))
        {
            GiveUpControl(firstPos);
            playerState.ClearCards();
            throw new FlipException("Card is already controlled.");
        }

        // Rule 2-C: Turn cards facing down to facing up
        if (!cell.IsUp)
            cell.TurnUp();

        if (firstCell.Card != cell.Card)
        {
            // Rule 2-E: No match => give up control of both cards, they remain face up
            GiveUpControl(firstPos);
            playerState.SetSecondCard(pos);
        }
        else
        {
            // Rule 2-D: Match => keep control of both cards, they remain face up
            TakeControl(playerId, pos);
            playerState.SetSecondCard(pos);
        }
    }

    private void CleanupPreviousMove(string playerId)
    {
        var playerState = _players[playerId];
        var firstPos = playerState.FirstCard!.Value;
        var secondPos = playerState.SecondCard!.Value;

        var firstCell = _grid[firstPos.Row, firstPos.Column];
        var secondCell = _grid[secondPos.Row, secondPos.Column];

        bool matched = firstCell.Card != null && secondCell.Card != null
                       && firstCell.Card == secondCell.Card;

        if (matched)
        {
            // Rule 3-A: Remove matching pair
            if (IsControlledBy(firstPos, playerId))
            {
                firstCell.Remove();
                GiveUpControl(firstPos);
            }

            if (IsControlledBy(secondPos, playerId))
            {
                secondCell.Remove();
                GiveUpControl(secondPos);
            }
        }
        else
        {
            // Rule 3-B: Turn down non-matching cards if conditions are met
            TurnDownIfPossible(firstPos);
            TurnDownIfPossible(secondPos);
        }

        playerState.ClearCards();
    }

    private void TurnDownIfPossible((int Row, int Column) pos)
    {
        var cell = _grid[pos.Row, pos.Column];

        // Rule 3-B: Turn face down if:
        // - Card still exists
        // - Card is face up
        // - Card is not controlled by another player
        if (cell.Card != null && cell.IsUp && !IsControlled(pos))
            cell.TurnDown();
    }
}

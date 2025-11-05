using MemoryScramble.API.Exceptions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;

namespace PR_lab3_MemoryScramble;

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

    public string Look(string playerId)
    {
        throw new NotImplementedException();
    }

    public string Flip(string playerId, int row, int column)
    {
        throw new NotImplementedException();
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
                    Debug.Assert(CardRegex.IsMatch(cell.Card), $"Invalid card string at ({i},{j}): '{cell.Card}'");
            }

        foreach (var kvp in _controlledBy)
        {
            var pos = kvp.Key;
            var pid = kvp.Value;

            var row = pos.Row;
            var col = pos.Column;

            Debug.Assert(row >= 0 && row < Rows && col >= 0 && col < Columns, "Controlled card out of bounds.");

            var cell = _grid[row, col];
            Debug.Assert(cell.Card != null, "Controlled card must exist.");
            Debug.Assert(cell.IsUp, "Controlled card must be face up.");

            Debug.Assert(!string.IsNullOrEmpty(pid), "ControlledBy contains empty player id.");
            Debug.Assert(_players.ContainsKey(pid), $"_controlledBy references unknown player '{pid}'.");

            var pstate = _players[pid];
            bool inPlayerState = (pstate.FirstCard != null && pstate.FirstCard.Value == (row, col))
                                 || (pstate.SecondCard != null && pstate.SecondCard.Value == (row, col));
            Debug.Assert(inPlayerState, $"_controlledBy position ({row},{col}) not present in player {pid}'s state.");
        }

        foreach (var kvp in _players)
        {
            var pid = kvp.Key;
            var st = kvp.Value;

            Debug.Assert(!(st.FirstCard == null && st.SecondCard != null), $"Player {pid} has SecondCard but no FirstCard.");

            if (st.FirstCard != null)
            {
                var (r, c) = st.FirstCard.Value;
                Debug.Assert(r >= 0 && r < Rows && c >= 0 && c < Columns, $"Player {pid}'s FirstCard out of bounds.");
                var cell = _grid[r, c];
                Debug.Assert(cell.Card != null && cell.IsUp, $"Player {pid}'s FirstCard must be a real, face-up card.");
                Debug.Assert(_controlledBy.ContainsKey((r, c)) && _controlledBy[(r, c)] == pid,
                    $"Player {pid}'s FirstCard must be controlled by that player.");
            }

            if (st.SecondCard != null)
            {
                var (r, c) = st.SecondCard.Value;
                Debug.Assert(r >= 0 && r < Rows && c >= 0 && c < Columns, $"Player {pid}'s SecondCard out of bounds.");
                var cell = _grid[r, c];
                Debug.Assert(cell.Card != null && cell.IsUp, $"Player {pid}'s SecondCard must be a real, face-up card.");
                Debug.Assert(_controlledBy.ContainsKey((r, c)) && _controlledBy[(r, c)] == pid,
                    $"Player {pid}'s SecondCard must be controlled by that player.");
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
}

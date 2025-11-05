namespace PR_lab3_MemoryScramble;

public class Board
{
    private class Cell(string? card)
    {
        public string? Card { get; set; } = card;
        public bool IsUp { get; set; }
    }

    private readonly Cell[,] _grid;
    private readonly Dictionary<(int Row, int Column), string> _controlledBy;


    public int Rows { get; init; }
    public int Columns { get; init; }


    private Board(int rows, int columns, Cell[,] grid) 
    {
        Rows = rows;
        Columns = columns;
        _grid = grid;
        _controlledBy = new();
        CheckRep();
    }

    public static async Task<Board> ParseFromFile(string fileName)
    {
        throw new NotImplementedException();
    }

    public string Look(string playerId)
    {
        throw new NotImplementedException();
    }

    public string Flip(string playerId, int row, int column)
    {
        throw new NotImplementedException();
    }

    private void CheckRep()
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        throw new NotImplementedException();
    }
}

namespace ControllerMouse.Core;

public readonly record struct KeyboardKey(int Id, double Width = 1);
/// <summary>Spatial selection without taking keyboard focus away from the text editor.</summary>
public sealed class KeyboardSelection
{
    private KeyboardKey[][] _rows = [];
    public int Row { get; private set; }
    public int Column { get; private set; }
    public int Selected => _rows.Length == 0 ? 0 : _rows[Row][Column].Id;
    public void SetRows(KeyboardKey[][] rows, int selected = 0x1b)
    {
        if (rows.Length == 0 || rows.Any(r => r.Length == 0 || r.Any(k => k.Width <= 0 || !double.IsFinite(k.Width)))) throw new ArgumentException("Keyboard rows must contain valid keys.");
        _rows = rows.Select(r => r.ToArray()).ToArray(); Row = Column = 0; Select(selected);
    }
    public void Select(int id)
    {
        for (int r = 0; r < _rows.Length; r++) for (int c = 0; c < _rows[r].Length; c++)
            if (_rows[r][c].Id == id) { Row = r; Column = c; return; }
    }
    private double Centre(int row, int column) => (_rows[row].Take(column).Sum(k => k.Width) + _rows[row][column].Width / 2) / _rows[row].Sum(k => k.Width);
    public int Move(int horizontal, int vertical)
    {
        if (_rows.Length == 0) return Selected;
        if (horizontal != 0) Column = (Column + Math.Sign(horizontal) + _rows[Row].Length) % _rows[Row].Length;
        if (vertical != 0)
        {
            double centre = Centre(Row, Column); int next = Math.Clamp(Row + Math.Sign(vertical), 0, _rows.Length - 1);
            Column = Enumerable.Range(0, _rows[next].Length).MinBy(c => Math.Abs(Centre(next, c) - centre)); Row = next;
        }
        return Selected;
    }
    /// <summary>One switch steps through every key and toolbar action in reading order.</summary>
    public int MoveSequential(int direction)
    {
        if (_rows.Length == 0 || direction == 0) return Selected;
        Column += Math.Sign(direction);
        if (Column >= _rows[Row].Length) { Row = (Row + 1) % _rows.Length; Column = 0; }
        else if (Column < 0) { Row = (Row + _rows.Length - 1) % _rows.Length; Column = _rows[Row].Length - 1; }
        return Selected;
    }
}

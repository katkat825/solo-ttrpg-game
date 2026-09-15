using Godot;
using Core.Space;

namespace Game.Board
{
    public sealed class BoardMetrics
    {
        public int Columns { get; }

        public int Rows { get; }

        // one square edge to edge, in metres; 60 mm reads as a battle map beside 50 mm dice
        public float CellSize { get; }

        public BoardMetrics(int columns, int rows, float cellSize)
        {
            Columns = columns;
            Rows = rows;
            CellSize = cellSize;
        }

        public float Width => Columns * CellSize;

        public float Depth => Rows * CellSize;

        public float HalfWidth => Width * 0.5f;

        public float HalfDepth => Depth * 0.5f;

        // centred on its own origin, so the node's position is the map's middle and Y=0 is the mat surface
        public Vector3 Centre(Cell cell) => new Vector3(
            (cell.X + 0.5f) * CellSize - HalfWidth,
            0f,
            (cell.Y + 0.5f) * CellSize - HalfDepth);

        // middle of a line between squares; arithmetic, so a boundary line off the map still has a place
        public Vector3 Centre(Border border) => Centre(border.Cell) - (border.Vertical
            ? new Vector3(CellSize * 0.5f, 0f, 0f)
            : new Vector3(0f, 0f, CellSize * 0.5f));

        // height ignored; and it can name a square off the board on purpose, refused not clamped
        public Cell At(Vector3 local) => new Cell(
            Mathf.FloorToInt((local.X + HalfWidth) / CellSize),
            Mathf.FloorToInt((local.Z + HalfDepth) / CellSize));

        public bool IsUsable => Columns > 0 && Rows > 0 && CellSize > 0f;

        // 8x8 is only the fallback empty room; CellSize is the real default, a property of the table
        public static readonly BoardMetrics Shipped = new BoardMetrics(8, 8, 0.06f);

        // developer only, not localized, must never reach the screen
        public override string ToString() =>
            $"{Columns} x {Rows} squares of {CellSize * 1000f:0} mm, board {Width:0.000} x {Depth:0.000}";
    }
}

using Godot;
using Core.Space;

namespace Game.Board
{
    // how big the board is and where a square lies on it, in the board's OWN space - the one
    // place that answers it, and the board's half of what TrayBounds does for the tray.
    //
    // THE SAME DISCIPLINE F2 ESTABLISHED, NOW FOR THE BOARD (THE_BOARD.md B0). Every position
    // here is measured from the board node's own origin, never from the world's, so the board can
    // be stood anywhere on the table - beside the tray today, somewhere else when the DM screen
    // arrives - and every square still lands where it should. The tray had four world-space
    // constants in three files and could only ever sit at the origin; the board starts without
    // that mistake rather than repeating it.
    //
    // Godot-free apart from Vector3, exactly as TrayBounds is apart from Vector2, so game.tests
    // can hold the derivations without an engine behind them.
    //
    // WHAT IT IS NOT: it holds no occupancy and no terrain. Core.Space.Grid says which squares
    // exist and who is standing on them, MapLayout says what each one is made of, and this says
    // where any of it is on the table. The extent is stated once - in the map file, by the shape
    // of the room drawn in it - and all three are built from that.
    public sealed class BoardMetrics
    {
        public int Columns { get; }

        public int Rows { get; }

        // one square, edge to edge, in metres. 60 mm reads as a battle map beside 50 mm dice -
        // a real 1-inch grid would put a mini half the height of a d12 next to it
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

        // the board is centred on its own origin, so the node's position IS where the middle of
        // the map sits. that is the arrangement that makes it movable: nothing downstream has to
        // know a corner
        //
        // Y is the playing surface, at the board's own zero - the top of the mat, not its middle,
        // the same way TrayBounds.FeltY is the top of the floor box. A mini standing here has its
        // feet on the felt
        public Vector3 Centre(Cell cell) => new Vector3(
            (cell.X + 0.5f) * CellSize - HalfWidth,
            0f,
            (cell.Y + 0.5f) * CellSize - HalfDepth);

        // which square a point on the mat falls in. HEIGHT IS IGNORED: a click arrives as a ray
        // that has already been met with the surface, and a piece being lifted is still over the
        // square it left
        //
        // this can name a square the board does not have, and that is deliberate - a click past
        // the edge has to be REFUSED rather than clamped to the nearest square, or the far edge
        // of the map becomes a magnet the width of the table. Grid.Contains is what refuses it
        public Cell At(Vector3 local) => new Cell(
            Mathf.FloorToInt((local.X + HalfWidth) / CellSize),
            Mathf.FloorToInt((local.Z + HalfDepth) / CellSize));

        // true when these numbers describe a board a piece could actually stand on
        public bool IsUsable => Columns > 0 && Rows > 0 && CellSize > 0f;

        // the size of a square the game ships with, and the room the board falls back to when the
        // map file cannot be read at all.
        //
        // THE EXTENT HERE IS NOT THE GAME'S EXTENT ANY MORE. Since B2 the room comes from the map
        // file and states its own width by being that wide, so these 8 x 8 are only the empty room
        // Board draws while you read the error that says why. CellSize is the real default: how big
        // a square is belongs to the table, not to the room, and the same map is the same map
        // whatever size mat it is played on
        public static readonly BoardMetrics Shipped = new BoardMetrics(8, 8, 0.06f);

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            $"{Columns} x {Rows} squares of {CellSize * 1000f:0} mm, board {Width:0.000} x {Depth:0.000}";
    }
}

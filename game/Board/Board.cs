using Godot;
using Core.Space;

namespace Game.Board
{
    // the battle map on the table: a grid of squares, and the pieces standing on them.
    //
    // THE VIEW OF A MODEL IT DOES NOT OWN. Core.Space.Grid says which squares exist and who is on
    // them; BoardMetrics says where those squares are; this draws them and turns a click into a
    // move. Exactly the arrangement the tray has with PoolResult, and the reason THE_BOARD.md
    // puts the spatial model in core/: a grid is geometry, geometry is testable headless, and the
    // board being simulatable is what lets a future balance run give enemies positions.
    //
    // The visible board is BUILT, not authored. Squares come from the extent, which is stated
    // once - a scene full of hand-placed tiles would be a second description of the grid, free to
    // disagree with it, and B2 replaces this placeholder mat with tiles read from a data file
    // anyway. Placeholder felt-and-lines, the way the d6 started as a BoxMesh.
    //
    // NO UI. A refused click does nothing visible yet and says so only in the output - THE_TABLE
    // .md 6, the room is the entire UI, and B3 is where a refusal has to read on the board itself.
    public partial class Board : Node3D
    {
        // the extent, and the only statement of it. BoardMetrics.Shipped holds the defaults;
        // board.tscn does not restate them, so there is no second copy to drift
        [Export] public int Columns { get; set; } = 8;

        [Export] public int Rows { get; set; } = 8;

        [Export] public float CellSize { get; set; } = 0.06f;

        // the mat and the painted grid. both authored in board.tscn, because what the board is
        // made of is art direction and belongs where it can be looked at
        [Export] public Material Mat { get; set; }

        [Export] public Material Lines { get; set; }

        [Export] public NodePath PiecePath { get; set; } = "Mini";

        // where the piece starts. Vector2I rather than Cell because Godot exports its own types;
        // it becomes a Cell the moment it is read and never travels as a pair of ints
        [Export] public Vector2I StartCell { get; set; } = new Vector2I(3, 4);

        // ---- the mat, in metres ----

        const float MatThickness = 0.006f;

        // painted lines, not grooves - a shade proud of the felt so they never z-fight with it
        const float LineWidth = 0.0022f;

        const float LineHeight = 0.0010f;

        const float LineRise = 0.0003f;

        BoardMetrics _metrics = BoardMetrics.Shipped;

        Grid<Mini> _grid;

        Mini _piece;

        public BoardMetrics Metrics => _metrics;

        public Grid<Mini> Squares => _grid;

        public override void _Ready()
        {
            _metrics = Measure();

            // both built from the same numbers, in that order, so the drawing and the model can
            // never describe two different boards
            _grid = new Grid<Mini>(_metrics.Columns, _metrics.Rows);

            Build();

            _piece = GetNodeOrNull<Mini>(PiecePath);

            if (_piece == null)
            {
                GD.PushError($"board: no Mini at '{PiecePath}' - the board is a mat with nothing on it");
                return;
            }

            var start = new Cell(StartCell.X, StartCell.Y);

            if (!_grid.Place(_piece, start))
            {
                GD.PushError($"board: {start} is not a square on a {_metrics} - starting the piece at (0, 0)");
                start = new Cell(0, 0);
                _grid.Place(_piece, start);
            }

            _piece.PlaceAt(_metrics.Centre(start));

            // developer diagnostics, not player-facing text
            GD.Print($"board   {_metrics}");
            GD.Print($"piece   {_piece.Name} on {start} - click a square to move it");
            GD.Print($"minis   {_piece.Voice}");
        }

        // the exports are what the board is built from; this is the one place they are checked.
        // a board with no squares, or squares with no size, draws nothing and swallows every
        // click, which looks exactly like the board being missing - so it says so and falls back
        // to the authored one, the same way DiceTray does with an unmeasurable tray
        BoardMetrics Measure()
        {
            var authored = new BoardMetrics(Columns, Rows, CellSize);

            if (authored.IsUsable) return authored;

            GD.PushError($"board: {authored} is not a board a piece could stand on - " +
                         $"falling back to {BoardMetrics.Shipped}");

            return BoardMetrics.Shipped;
        }

        // everything visible, under one node, so B2 can throw the whole placeholder away by
        // replacing this method
        void Build()
        {
            var under = new Node3D { Name = "Surface" };
            AddChild(under);

            if (Mat == null || Lines == null)
                GD.PushError("board: the mat or the grid lines have no material - the board will be untextured");

            // the top of the mat is the board's own zero, so a piece standing on a square has its
            // feet at y 0 and BoardMetrics.Centre needs no offset to account for the thickness
            under.AddChild(new MeshInstance3D
            {
                Name = "Mat",
                Mesh = new BoxMesh { Size = new Vector3(_metrics.Width, MatThickness, _metrics.Depth) },
                MaterialOverride = Mat,
                Position = new Vector3(0f, -MatThickness * 0.5f, 0f),
            });

            // one mesh for every line down the board and one for every line across it, shared
            // between the instances that use it
            var downwards = new BoxMesh { Size = new Vector3(LineWidth, LineHeight, _metrics.Depth + LineWidth) };
            var across = new BoxMesh { Size = new Vector3(_metrics.Width + LineWidth, LineHeight, LineWidth) };

            // one more line than squares in each direction - the outside edges are painted too,
            // which is what makes the map a map rather than a pattern
            for (int x = 0; x <= _metrics.Columns; x++)
                under.AddChild(Line($"Down{x:00}", downwards,
                    new Vector3(x * _metrics.CellSize - _metrics.HalfWidth, LineRise, 0f)));

            for (int y = 0; y <= _metrics.Rows; y++)
                under.AddChild(Line($"Across{y:00}", across,
                    new Vector3(0f, LineRise, y * _metrics.CellSize - _metrics.HalfDepth)));
        }

        MeshInstance3D Line(string name, Mesh mesh, Vector3 at) => new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = Lines,
            Position = at,
        };

        // a click on a square moves the piece to it. no reachability, no turns, no whose-move-is
        // -it - that is COMBAT_LOOP.md, and B1 says any square is one move away
        public override void _UnhandledInput(InputEvent @event)
        {
            if (!@event.IsActionPressed("place_piece") || _piece == null) return;

            Cell? clicked = CellUnder(@event);

            if (clicked == null) return;

            GetViewport().SetInputAsHandled();

            Cell cell = clicked.Value;
            Cell? standing = _grid.CellOf(_piece);

            // clicking the square it is already on is not a move. sliding nowhere would still
            // hesitate and click, which reads as the board answering a question nobody asked
            if (standing == cell) return;

            if (!_grid.Move(_piece, cell))
            {
                // B3 makes this legible on the board itself. today the only way to reach it is
                // to put a second piece out, and it is here so that when that happens the model
                // has already refused rather than the view having to remember to ask
                GD.Print($"board   {cell} is taken - the piece stays on {standing}");
                return;
            }

            _piece.SlideTo(_metrics.Centre(cell));
        }

        // which square the mouse is over, or null for a click that missed the board
        //
        // MET WITH THE BOARD'S OWN SURFACE PLANE rather than with a collision body: the board is
        // flat and infinite in its own plane, so this is exact, needs no physics, and cannot be
        // fooled by a piece standing between the camera and the felt. Off the mat comes back as a
        // square the grid does not have, and is refused there rather than clamped to the nearest
        // one - a clamp would make the far edge a magnet the width of the table
        Cell? CellUnder(InputEvent @event)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return null;

            Vector2 at = @event is InputEventMouse mouse ? mouse.Position : GetViewport().GetMousePosition();

            var surface = new Plane(GlobalTransform.Basis.Y.Normalized(), GlobalTransform.Origin);

            Vector3? met = surface.IntersectsRay(camera.ProjectRayOrigin(at), camera.ProjectRayNormal(at));

            if (met == null) return null;

            Cell cell = _metrics.At(ToLocal(met.Value));

            return _grid.Contains(cell) ? cell : null;
        }
    }
}

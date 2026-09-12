using System.Collections.Generic;
using Godot;
using Core.Space;

namespace Game.Board
{
    // the battle map on the table: a map read from a file, and the pieces standing on it.
    //
    // THE VIEW OF A MODEL IT DOES NOT OWN. `MapLayout` says what every square is made of,
    // `Core.Space.Grid` says who is standing on them, `Route` says whether there is a way there,
    // and `BoardMetrics` says where any of it is on this table. This draws them and turns a click
    // into a move. Exactly the arrangement the tray has with `PoolResult`, and the reason
    // THE_BOARD.md puts the spatial model in core/: geometry is testable headless, and the board
    // being simulatable is what lets a future balance run give enemies positions.
    //
    // THE MAP IS DATA (B2). Nothing about this room is in this file or in board.tscn - the extent,
    // the walls, the door, the rubble and where the hero starts all come out of maps/cellar.map,
    // and moving a wall is one character in that file. What is left here is the same in every room
    // there will ever be: how a square is drawn, and what a click means.
    //
    // NO UI (B3). A refused move is shown ON THE BOARD - the square lights and the piece leans at
    // it and settles back - because THE_TABLE.md 6 says the room is the entire UI and there is no
    // message box on a table.
    public partial class Board : Node3D
    {
        // WHICH ROOM. the one thing about the map that a scene gets to say, because a second board
        // in a second scene is a second map and not a second copy of this code. Phase P replaces
        // this with a campaign package naming its own maps (ARCHITECTURE.md 4)
        [Export(PropertyHint.File, "*.map")] public string MapPath { get; set; } = "res://maps/cellar.map";

        // how big a square is on THIS table, which is a property of the table and not of the map -
        // the same room is the same room whether it is played on a small mat or a large one
        [Export] public float CellSize { get; set; } = 0.06f;

        // the mat and the painted grid, then one material per kind of terrain. all authored in
        // board.tscn, because what the board is made of is art direction and belongs where it can
        // be looked at
        [Export] public Material Mat { get; set; }

        [Export] public Material Lines { get; set; }

        [Export] public Material Wall { get; set; }

        [Export] public Material Door { get; set; }

        [Export] public Material Rough { get; set; }

        // what a square looks like when the answer is no. dusty red, and mostly transparent: it is
        // ink on a map rather than a light under it
        [Export] public Color RefusedTint { get; set; } = new Color(0.62f, 0.18f, 0.14f, 0.5f);

        [Export] public NodePath PiecePath { get; set; } = "Mini";

        // ---- the mat, in metres ----

        const float MatThickness = 0.006f;

        // painted lines, not grooves - a shade proud of the mat so they never z-fight with it
        const float LineWidth = 0.0022f;

        const float LineHeight = 0.0010f;

        const float LineRise = 0.0003f;

        BoardMetrics _metrics = BoardMetrics.Shipped;

        MapLayout _map;

        Grid<Mini> _grid;

        Mini _piece;

        CellFlash _refused;

        public BoardMetrics Metrics => _metrics;

        public MapLayout Map => _map;

        public Grid<Mini> Squares => _grid;

        public override void _Ready()
        {
            _map = Load();

            // all three built from the map, in that order, so the drawing, the model and the room
            // can never describe three different boards
            _metrics = new BoardMetrics(_map.Columns, _map.Rows, Usable(CellSize));
            _grid = new Grid<Mini>(_map.Columns, _map.Rows);

            Build();

            _piece = GetNodeOrNull<Mini>(PiecePath);

            if (_piece == null)
            {
                GD.PushError($"board: no Mini at '{PiecePath}' - the board is a map with nothing on it");
                return;
            }

            Cell start = _map.Start;

            if (!_grid.Place(_piece, start))
            {
                GD.PushError($"board: {start} is not a square on a {_metrics} - starting the piece at (0, 0)");
                start = new Cell(0, 0);
                _grid.Place(_piece, start);
            }

            _piece.PlaceAt(_metrics.Centre(start));

            // developer diagnostics, not player-facing text
            GD.Print($"board   {_metrics}");
            GD.Print($"map     {_map} - {MapPath}");
            GD.Print($"piece   {_piece.Name} on {start} - click a square to move it");
            GD.Print($"minis   {_piece.Voice}");
        }

        // THE MAP FILE IS THE MAP, and a broken one has to say so rather than half-load. A room
        // with a wall missing and nothing to tell you is the failure this whole path exists to
        // make impossible, so every refusal is printed with the line in the file that caused it.
        //
        // Godot's FileAccess rather than System.IO, because in an exported game the map is inside
        // a .pck and nothing else can reach it. core/ never opens a file: it parses the text this
        // hands it, which is what keeps the reader testable headless.
        MapLayout Load()
        {
            string text = FileAccess.FileExists(MapPath) ? FileAccess.GetFileAsString(MapPath) : null;

            if (text == null)
            {
                GD.PushError($"board: there is no map at '{MapPath}' - " +
                             $"falling back to an empty room, {BoardMetrics.Shipped}");
                return EmptyRoom();
            }

            if (MapReader.TryRead(text, out MapLayout map, out string problem)) return map;

            GD.PushError($"board: {MapPath} is not a map - {problem}. " +
                         $"falling back to an empty room, {BoardMetrics.Shipped}");

            return EmptyRoom();
        }

        // four walls and nothing else, at the size the game ships with. NOT a map anyone plays on -
        // it is what the board is while the error above is being read, and it exists so a mistyped
        // map leaves a board you can see is wrong rather than a black table and a piece nowhere
        static MapLayout EmptyRoom()
        {
            int columns = BoardMetrics.Shipped.Columns;
            int rows = BoardMetrics.Shipped.Rows;

            var tiles = new Tile[columns * rows];

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    tiles[y * columns + x] =
                        x == 0 || y == 0 || x == columns - 1 || y == rows - 1 ? Tile.Wall : Tile.Floor;

            return new MapLayout(columns, rows, tiles, new Cell(columns / 2, rows / 2));
        }

        float Usable(float cell)
        {
            if (cell > 0f) return cell;

            GD.PushError($"board: a square {cell} across is not a square - " +
                         $"using the authored {BoardMetrics.Shipped.CellSize}");

            return BoardMetrics.Shipped.CellSize;
        }

        // everything visible, under one node. the mat and its grid are the wet-erase map; the
        // terrain standing on it is BoardTiles' business
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

            var terrain = new Node3D { Name = "Terrain" };
            AddChild(terrain);

            new BoardTiles(_metrics) { Wall = Wall, Door = Door, Rough = Rough }.Build(terrain, _map);

            AddChild(_refused = new CellFlash
            {
                Name = "Refused",
                Tint = RefusedTint,
                Square = new BoxMesh
                {
                    Size = new Vector3(_metrics.CellSize - LineWidth, 0.0004f, _metrics.CellSize - LineWidth),
                },
            });
        }

        MeshInstance3D Line(string name, Mesh mesh, Vector3 at) => new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = Lines,
            Position = at,
        };

        // a click on a square walks the piece there, if there is a way. no reachability limit, no
        // turns, no whose-move-is-it - that is COMBAT_LOOP.md. Here every square the piece can
        // reach is one move away, however far round the room that is
        public override void _UnhandledInput(InputEvent @event)
        {
            if (!@event.IsActionPressed("place_piece") || _piece == null) return;

            Cell? clicked = CellUnder(@event);

            if (clicked == null) return;

            GetViewport().SetInputAsHandled();

            Cell cell = clicked.Value;
            Cell? standing = _grid.CellOf(_piece);

            if (standing == null) return;

            // clicking the square it is already on is not a move. leaning and setting down again
            // would read as the board answering a question nobody asked
            if (standing == cell) return;

            IReadOnlyList<Cell> route =
                Route.Between(_map, standing.Value, cell, taken => _grid.IsOccupied(taken));

            if (route == null)
            {
                Refuse(cell, standing.Value);
                return;
            }

            // THE MODEL MOVES FIRST AND THE VIEW CATCHES UP. by the time the piece starts walking,
            // the grid already has it on the destination, so an interrupted move cannot leave the
            // two disagreeing about where it is
            if (!_grid.Move(_piece, cell))
            {
                Refuse(cell, standing.Value);
                return;
            }

            _piece.Follow(Waypoints(route));

            // developer diagnostics. sight is not drawn anywhere yet - B3 builds it for the fight
            // that comes next - so this is where it can be watched working
            GD.Print($"move    {standing} to {cell}, {route.Count - 1} squares, " +
                     $"{Route.Cost(_map, route)} to walk" +
                     (Sight.Clear(_map, standing.Value, cell) ? "" : " - out of sight, round a corner"));
        }

        // no way there: say so on the board and leave the piece where it is. the two halves are
        // one gesture - somebody tapping the square, and the piece leaning at it and settling back
        void Refuse(Cell cell, Cell standing)
        {
            _refused?.Show(_metrics.Centre(cell));
            _piece.Refuse(_metrics.Centre(cell));

            GD.Print($"board   no way to {cell} from {standing} - " +
                     $"{_map.At(cell)}{(_grid.IsOccupied(cell) ? ", and taken" : "")}");
        }

        List<Vector3> Waypoints(IReadOnlyList<Cell> route)
        {
            var through = new List<Vector3>(route.Count);

            foreach (Cell cell in route) through.Add(_metrics.Centre(cell));

            return through;
        }

        // which square the mouse is over, or null for a click that missed the board
        //
        // MET WITH THE BOARD'S OWN SURFACE PLANE rather than with a collision body: the board is
        // flat and infinite in its own plane, so this is exact, needs no physics, and a piece
        // standing on a square never intercepts a click meant for it. What it does mean is that
        // the answer is always the square of MAT under the pointer - so clicking the top of a wall
        // block picks the square just beyond it, which is the honest reading of a flat map with
        // terrain standing on it and is worth revisiting in B5 when the terrain is real.
        //
        // Off the mat comes back as a square the grid does not have, and is refused there rather
        // than clamped to the nearest one - a clamp would make the far edge a magnet the width of
        // the table
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

using System;
using System.Collections.Generic;
using Godot;
using Core.Characters;
using Core.Resolution;
using Core.Space;
using Game.Tray;

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

        // THE TERRAIN ITSELF (B5). Culled out of the KayKit dungeon pack by tools/pull-models.ps1,
        // wearing the game's palette and the painted-miniature shader. Left empty, the board falls
        // back to the boxes above - which is how B2, B3 and B4 were verified and is worth keeping
        // able to come back
        [Export] public PackedScene WallModel { get; set; }

        [Export] public PackedScene DoorwayModel { get; set; }

        [Export] public PackedScene RubbleModel { get; set; }

        // one material for every model on the board: the whole pack shares one atlas
        [Export] public Material Paint { get; set; }

        // what a square looks like when the answer is no. dusty red, and mostly transparent: it is
        // ink on a map rather than a light under it
        [Export] public Color RefusedTint { get; set; } = new Color(0.62f, 0.18f, 0.14f, 0.5f);

        [Export] public NodePath PiecePath { get; set; } = "Mini";

        // WHICH HERO. One id, and the roster behind it is still IArchetypeSource - so pointing
        // this at a campaign's own hero (Phase P) or at a character the player made (Phase R) is
        // this line and nothing else. It is an export because C5 needs to be able to look at
        // channelling, and the Barbarian is untrained in it: set this to "mage" and the same
        // board plays a caster
        [Export] public string HeroId { get; set; } = EngineIds.Barbarian;

        // WHERE THE DICE ARE. the board does not own a tray and must not - it asks the one on the
        // table to throw a pool and listens for the answer, which is the same seam the tray
        // already has with the rules. Set in table.tscn, because only the table knows both are
        // standing on it; left empty, doors stay shut and the board says why
        [Export] public NodePath TrayPath { get; set; }

        // ---- the mat, in metres ----

        const float MatThickness = 0.006f;

        // painted lines, not grooves - a shade proud of the mat so they never z-fight with it
        const float LineWidth = 0.0022f;

        const float LineHeight = 0.0010f;

        const float LineRise = 0.0003f;

        BoardMetrics _metrics = BoardMetrics.Shipped;

        MapLayout _map;

        // THE ONE HERO, HARDCODED, exactly as Main.cs names one. Character creation is
        // ROOM_AND_SHEET.md and a roster read from a campaign is Phase P; naming the roster in one
        // place is what keeps that swap to this line
        readonly IArchetypeSource _archetypes = new BuiltInArchetypes();

        Actor _hero;

        DiceTray _tray;

        Node3D _terrain;

        BoardTiles _tiles;

        // the door the hero is walking up to, or has just thrown at - a LINE between two squares
        // since EDGE_WALLS.md, not a square of its own. null when nothing is pending, which is
        // also what any other click sets it back to
        Border? _door;

        // true from the moment the dice leave the hand until the felt has been read. the hero is
        // busy; clicks wait
        bool _checking;

        // and readable from outside, because whoever else is running the table has to wait too -
        // a fight starting a swing while a door check is in the air would put two questions on
        // one throw
        public bool IsChecking => _checking;

        // the piece that was standing in the doorway, kept so it can be taken off the board again
        DoorPiece _opened;

        // which line that door was on, once it has been opened
        Border? _opening;

        // and the square its wreckage landed in, if it gave way rather than swinging
        Cell? _wreckage;

        Grid<Mini> _grid;

        Mini _piece;

        CellFlash _refused;

        public BoardMetrics Metrics => _metrics;

        public MapLayout Map => _map;

        public Grid<Mini> Squares => _grid;

        // the hero's own piece, and the hero it stands for. ONE of each on the table: the fight
        // (COMBAT_LOOP.md C0) drives the same Actor the door check damages, so a hero who forced
        // a door the hard way walks into the room already Winded
        public Mini Piece => _piece;

        public Actor Hero => _hero;

        // WHOEVER IS RUNNING THE TABLE GETS FIRST REFUSAL ON A CLICK, and returning true means
        // they took it. Null is nobody, which is the board on its own - one piece walking round a
        // room, Phase B exactly as it was.
        //
        // A delegate and not an event, because this is not a notification: it is the answer to
        // "is somebody else deciding what a click means right now", and two answers to that would
        // be two things moving the same piece. A fight sets it in _Ready and clears it in
        // _ExitTree, and while it is set the board does not move anything of its own accord -
        // whose turn it is is a rule (CORE_RULES.md section 8) and the board owns no rules
        public Func<Cell, bool> Claims { get; set; }

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
            _piece.Arrived += OnArrived;

            _hero = Recruit();

            _tray = TrayPath == null || TrayPath.IsEmpty ? null : GetNodeOrNull<DiceTray>(TrayPath);

            if (_tray != null) _tray.Resolved += OnThrown;
            else if (TrayPath != null && !TrayPath.IsEmpty)
                GD.PushError($"board: no dice tray at '{TrayPath}' - a door has nothing to throw at it");

            // developer diagnostics, not player-facing text
            GD.Print($"board   {_metrics}");
            GD.Print($"map     {_map} - {MapPath}");
            GD.Print($"piece   {_piece.Name} on {start} - click a square to move it");
            GD.Print($"minis   {_piece.Voice}");
            GD.Print($"hero    {_hero} - {DoorCheck.Attribute} + {DoorCheck.Skill} + {_hero.WeaponId} " +
                     $"vs {DoorCheck.Against} at a shut door" +
                     (_tray == null ? " - NO TRAY WIRED UP, so doors stay shut" : ""));
            GD.Print($"board   {(char)KeyToRelock} puts the door back, for another go at it");
            GD.Print($"terrain {(WallModel == null ? "placeholder boxes" : "painted models")}" +
                     $"{(Paint == null && WallModel != null ? " - WITH NO PAINT ON THEM" : "")}");
        }

        // a hero the roster does not have is a scene naming somebody who does not exist, which
        // is worth saying rather than crashing on - the board falls back to the one it knows
        Actor Recruit()
        {
            if (_archetypes.Has(HeroId)) return _archetypes.Create(HeroId);

            GD.PushError($"board: there is no hero called '{HeroId}' in the roster " +
                         $"({string.Join(", ", _archetypes.Ids)}) - using {EngineIds.Barbarian}");

            return _archetypes.Create(EngineIds.Barbarian);
        }

        public override void _ExitTree()
        {
            if (_piece != null) _piece.Arrived -= OnArrived;
            if (_tray != null) _tray.Resolved -= OnThrown;
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
            var vertical = new Edge[(columns + 1) * rows];
            var horizontal = new Edge[columns * (rows + 1)];

            for (int i = 0; i < tiles.Length; i++) tiles[i] = Tile.Floor;

            // walled ON ITS OUTSIDE LINES rather than with a ring of solid squares, which is what
            // EDGE_WALLS.md made possible and is the honest way to draw a room: every square inside
            // is floor a piece can stand on, right up to the wall
            for (int y = 0; y < rows; y++)
            {
                vertical[y * (columns + 1)] = Edge.Wall;
                vertical[y * (columns + 1) + columns] = Edge.Wall;
            }

            for (int x = 0; x < columns; x++)
            {
                horizontal[x] = Edge.Wall;
                horizontal[rows * columns + x] = Edge.Wall;
            }

            return new MapLayout(columns, rows, tiles, new Cell(columns / 2, rows / 2),
                                 vertical, horizontal);
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

            _terrain = new Node3D { Name = "Terrain" };
            AddChild(_terrain);

            // kept, because terrain stopped being built once and forgotten in B4: a door forced
            // and a doorway full of wreckage are the same square redrawn from the map
            _tiles = new BoardTiles(_metrics)
            {
                Wall = Wall,
                Door = Door,
                Rough = Rough,
                WallModel = WallModel,
                DoorwayModel = DoorwayModel,
                RubbleModel = RubbleModel,
                Paint = Paint,
            };
            _tiles.Build(_terrain, _map);

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

        // a developer affordance rather than a game rule, and not an input action for the same
        // reason the tray's D, T and L are not: it is here to let both outcomes of one check be
        // seen in one sitting, and it does not survive past this phase
        const Key KeyToRelock = Key.R;

        // a click on a square walks the piece there, if there is a way. no reachability limit, no
        // turns, no whose-move-is-it - that is COMBAT_LOOP.md. Here every square the piece can
        // reach is one move away, however far round the room that is
        //
        // A SHUT DOOR IS THE EXCEPTION AND IT IS THE MILESTONE: clicking one sends the hero to it
        // and then to the dice.
        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: KeyToRelock })
            {
                Relock();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!@event.IsActionPressed("place_piece") || _piece == null) return;

            // the dice are in the air. the hero is busy, and a move begun now would be a piece
            // walking away from its own check
            if (_checking) return;

            Cell? clicked = CellUnder(@event, out Vector3 local);

            if (clicked == null) return;

            GetViewport().SetInputAsHandled();

            Cell cell = clicked.Value;

            // a fight is on, and it decides what a square means - which foe is on it, whether the
            // hero has an action left, whether it is even his turn. the board answers WHICH SQUARE
            // and stops there, which is the whole of the seam
            if (Claims != null && Claims(cell)) return;

            Cell? standing = _grid.CellOf(_piece);

            if (standing == null) return;

            // whatever was being walked toward, this click replaces it
            _door = null;

            // A DOOR IS A LINE NOW, so clicking one means clicking NEAR one - the pointer lands in
            // a square either side of it and the door is the nearest line to where it landed.
            // That is also what it looks like: the door is drawn on that line, and you clicked it
            Border? door = DoorNear(local) ?? ShutDoorBeside(cell, standing.Value);

            if (door != null)
            {
                Approach(door.Value, standing.Value);
                return;
            }

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

        // ---- pieces on the board, for whoever is running the table (COMBAT_LOOP.md C0) ----

        // another piece, standing on a square. the board owns what is standing where, so a fight
        // that wanted to add its own minis to the scene and keep its own idea of who is on which
        // square would be a second occupancy map free to disagree with this one
        //
        // null when the square is off the board, solid, or already taken - the same three
        // refusals a move gets, and for the same reasons
        public Mini Place(PackedScene piece, Cell cell, string name)
        {
            if (piece == null) return null;

            if (!_grid.Contains(cell) || !_map.IsPassable(cell) || _grid.IsOccupied(cell))
            {
                GD.PushError($"board: nothing can stand on {cell} - " +
                             (!_grid.Contains(cell) ? "it is not a square on this board"
                              : !_map.IsPassable(cell) ? _map.At(cell).ToString()
                              : "somebody is already there"));
                return null;
            }

            var mini = piece.Instantiate<Mini>();

            if (mini == null)
            {
                GD.PushError($"board: '{name}' is not a Mini scene - nothing placed on {cell}");
                return null;
            }

            mini.Name = name;
            AddChild(mini);

            _grid.Place(mini, cell);
            mini.PlaceAt(_metrics.Centre(cell));

            return mini;
        }

        // off the board and out of the scene - what happens to a piece that has been removed
        public void Lift(Mini piece)
        {
            if (piece == null) return;

            _grid.Remove(piece);
            piece.QueueFree();
        }

        public Cell? CellOf(Mini piece) => _grid.CellOf(piece);

        // walk a piece to a square, by the same rules a click walks the hero: a route or nothing.
        // false means there was no way there, and the board has already said so on the board
        public bool Walk(Mini piece, Cell to)
        {
            Cell? standing = _grid.CellOf(piece);

            if (piece == null || standing == null) return false;
            if (standing == to) return true;

            IReadOnlyList<Cell> route = Route.Between(_map, standing.Value, to, Taken);

            if (route == null || !_grid.Move(piece, to))
            {
                Refuse(piece, to, standing.Value);
                return false;
            }

            piece.Follow(Waypoints(route));
            return true;
        }

        // no way there: say so on the board and leave the piece where it is. the two halves are
        // one gesture - somebody tapping the square, and the piece leaning at it and settling back
        public void Refuse(Cell cell, Cell standing) => Refuse(_piece, cell, standing);

        // whichever piece was asking. it was always the hero's until a fight put other pieces on
        // the board that also have somewhere they cannot go
        public void Refuse(Mini piece, Cell cell, Cell standing)
        {
            _refused?.Show(_metrics.Centre(cell));
            piece?.Refuse(_metrics.Centre(cell));

            // WHY there is no way there, which is three different answers now: the square is rock,
            // somebody is on it, or it is perfectly good floor with no open line to it
            string because = !_map.IsPassable(cell) ? _map.At(cell).ToString()
                : _grid.IsOccupied(cell) ? "taken"
                : "walled off from where the piece is standing";

            GD.Print($"board   no way to {cell} from {standing} - {because}");
        }

        // ---- the door, the dice, and what the felt decides (B4) ----

        // the nearest shut door to where the pointer landed, or null if it landed nowhere near one.
        //
        // A DOOR IS A LINE, and you cannot click a line - you click a square, near its edge. So the
        // door is found by distance from the point itself: within half a square of the line it is
        // drawn on, which is the near half of the square on either side. That is also what the eye
        // does, because the door is standing on exactly that line
        Border? DoorNear(Vector3 local)
        {
            Border? nearest = null;
            float closest = _metrics.CellSize * 0.5f;

            foreach (Border border in _map.Borders)
            {
                if (_map.At(border) != Edge.Door) continue;

                Vector3 to = _metrics.Centre(border) - local;
                float away = new Vector2(to.X, to.Z).Length();

                if (away >= closest) continue;

                closest = away;
                nearest = border;
            }

            return nearest;
        }

        // and the forgiving version: a click on a square there is no way to, with a shut door
        // between it and the hero. clicking the room BEYOND a door is what anybody does first, and
        // refusing it because the pointer was a centimetre past the line would be pedantry
        Border? ShutDoorBeside(Cell clicked, Cell standing)
        {
            if (clicked == standing || Route.Between(_map, standing, clicked, Taken) != null) return null;

            foreach (Border border in Round(clicked))
            {
                if (_map.At(border) != Edge.Door) continue;

                Cell beyond = border.Cell == clicked ? border.Across : border.Cell;

                if (Route.Between(_map, standing, beyond, Taken) != null) return border;
            }

            return null;
        }

        // the four lines round a square
        static IEnumerable<Border> Round(Cell cell)
        {
            yield return Border.North(cell);
            yield return Border.East(cell);
            yield return Border.South(cell);
            yield return Border.West(cell);
        }

        // the whole of B4, in the order it happens at a table: walk up to the door, then the dice
        // come out. The check is not thrown from across the room, because a check thrown from
        // across the room is a button.
        //
        // BOTH SQUARES BESIDE A DOOR ARE FLOOR NOW, so "up to the door" means either of them - and
        // that is more honest than the old approach to a door CELL, which the hero could never
        // actually stand on because the door was in it
        void Approach(Border door, Cell standing)
        {
            if (_tray == null)
            {
                GD.Print($"board   the door on {door} is shut and there is no tray to throw at it");
                Refuse(door.Cell, standing);
                return;
            }

            if (standing == door.Cell || standing == door.Across)
            {
                _door = door;
                Attempt();
                return;
            }

            IReadOnlyList<Cell> route = NearestApproach(door, standing);

            if (route == null)
            {
                // no way to stand beside it at all - a door on the far side of a sealed room
                Refuse(door.Cell, standing);
                return;
            }

            Cell arriving = route[route.Count - 1];

            _door = door;
            _grid.Move(_piece, arriving);
            _piece.Follow(Waypoints(route));

            GD.Print($"move    {standing} to {arriving}, up to the door on {door}");
        }

        // the cheaper of the two sides of the door
        IReadOnlyList<Cell> NearestApproach(Border door, Cell standing)
        {
            IReadOnlyList<Cell> best = null;

            foreach (Cell side in new[] { door.Cell, door.Across })
            {
                IReadOnlyList<Cell> route = Route.Between(_map, standing, side, Taken);

                if (route == null) continue;

                if (best == null || Route.Cost(_map, route) < Route.Cost(_map, best)) best = route;
            }

            return best;
        }

        bool Taken(Cell cell) => _grid.IsOccupied(cell);

        // the piece finished a move. if it was walking to a door, this is the moment
        void OnArrived()
        {
            if (_door != null) Attempt();
        }

        void Attempt()
        {
            Pool pool = DoorCheck.PoolFor(_hero);

            if (_tray == null || pool == null || pool.Count == 0)
            {
                _door = null;
                return;
            }

            _checking = true;

            GD.Print("");
            GD.Print($"check   the door on {_door} - {DoorCheck.Attribute} + {DoorCheck.Skill} + " +
                     $"{_hero.WeaponId}, {pool.Count} dice vs {DoorCheck.Against}");

            // EVERY BIT OF M0-M9 DOES THE REST. there is no second throwing path, no board dice,
            // and no number decided here - the tray throws the hero's own pool and the answer is
            // read off the felt with the rings and the names M6 already draws
            _tray.Throw(pool);
        }

        // the felt has stopped moving. the tray also throws for itself - space, and the shape tour -
        // so only an answer to a question this board asked is one it may act on
        void OnThrown(TrayThrow thrown)
        {
            if (!_checking || _door == null) return;

            _checking = false;

            Border door = _door.Value;
            _door = null;

            Apply(door, DoorCheck.Read(thrown));
        }

        void Apply(Border door, DoorOutcome outcome)
        {
            // the wreckage lands through the doorway, on the side the hero is not standing on
            Cell? standing = _grid.CellOf(_piece);
            Cell beyond = standing == door.Cell ? door.Across : door.Cell;

            // the door stops being terrain the moment it opens: renamed off the line so the line
            // can be redrawn as a gap without the redraw taking the door away mid-swing
            _opened = _terrain?.GetNodeOrNull<DoorPiece>(BoardTiles.NameFor(door));

            if (_opened != null)
            {
                _opened.Name = "OpenedDoor";
                _opened.Open(outcome.Forced);
            }

            // THE LINE OPENS EITHER WAY. failure is content, not a wall (CORE_RULES.md 0, pillar 4)
            _map = _map.With(door, Edge.None);
            _tiles.Update(_terrain, _map, door);

            _opening = door;
            _wreckage = null;

            // and what a failure leaves behind: the door in pieces on the floor of the room beyond,
            // which costs double to cross for the rest of the game. with squares on BOTH sides of a
            // line there is somewhere for it to land - when the door was a square of its own, there
            // was nowhere, and the wreckage had to be the doorway itself
            if (outcome.Leaves == Tile.Rough && _map.At(beyond) == Tile.Floor)
            {
                _map = _map.With(beyond, Tile.Rough);
                _tiles.Update(_terrain, _map, beyond);
                _wreckage = beyond;
            }

            // what it cost. the hero's own Vigor track, and the Conditions the rules already own -
            // a Winded hero throws a smaller Might die, which is the consequence turning up on the
            // felt next time rather than in a number nobody sees
            if (outcome.Cost > 0)
            {
                IReadOnlyList<Condition> taken = _hero.Damage(outcome.Cost);

                GD.Print($"hero    {_hero}" +
                         (taken.Count > 0 ? " - and takes " + string.Join(", ", taken) : ""));
            }

            GD.Print($"door    {outcome}");
            GD.Print(outcome.Forced
                ? $"        {door} is open and clear - the room behind it is reachable"
                : $"        {door} is open and {beyond} is full of wreckage - through, the hard way");
        }

        // put the door back, shut, and the hero back on his feet. a developer affordance: both
        // outcomes of one check in one sitting, without restarting the scene
        void Relock()
        {
            if (_opening == null)
            {
                GD.Print("board   no door has been opened yet - nothing to put back");
                return;
            }

            Border door = _opening.Value;

            if (_wreckage != null && _grid.IsOccupied(_wreckage.Value))
            {
                GD.Print($"board   the piece is standing in the wreckage on {_wreckage} - move it off first");
                return;
            }

            _opened?.QueueFree();
            _opened = null;

            _map = _map.With(door, Edge.Door);
            _tiles.Update(_terrain, _map, door);

            if (_wreckage != null)
            {
                _map = _map.With(_wreckage.Value, Tile.Floor);
                _tiles.Update(_terrain, _map, _wreckage.Value);
                _wreckage = null;
            }

            _opening = null;

            // A FIGHT IS HOLDING THIS HERO. Replacing him mid-fight would leave the encounter
            // swinging for an Actor nobody can see and the piece on the board standing for one
            // nobody is hitting. The door goes back either way - that is what this key is for
            if (Claims == null) _hero = Recruit();

            GD.Print("");
            GD.Print($"board   the door on {door} is shut again" +
                     (Claims == null ? $", and the hero is whole - {_hero}" : " - the hero keeps his wounds, a fight is on"));
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
        Cell? CellUnder(InputEvent @event, out Vector3 local)
        {
            local = Vector3.Zero;

            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return null;

            Vector2 at = @event is InputEventMouse mouse ? mouse.Position : GetViewport().GetMousePosition();

            var surface = new Plane(GlobalTransform.Basis.Y.Normalized(), GlobalTransform.Origin);

            Vector3? met = surface.IntersectsRay(camera.ProjectRayOrigin(at), camera.ProjectRayNormal(at));

            if (met == null) return null;

            // THE POINT COMES BACK TOO, not only the square it fell in. Since EDGE_WALLS.md a door
            // is a line rather than a square, and finding which line was clicked needs to know
            // where in the square the pointer actually landed
            local = ToLocal(met.Value);

            Cell cell = _metrics.At(local);

            return _grid.Contains(cell) ? cell : null;
        }
    }
}

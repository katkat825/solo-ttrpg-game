using System;
using System.Collections.Generic;
using Godot;
using Core.Characters;
using Core.Resolution;
using Core.Space;
using Game.Tray;

namespace Game.Board
{
    public partial class Board : Node3D
    {
        [Export(PropertyHint.File, "*.map")] public string MapPath { get; set; } = "res://maps/cellar.map";

        // an absolute path and a res:// path open the same way, so one line opens either
        [Export] public string Campaign { get; set; } = "";

        // MapName, not Map: Map is already the loaded MapLayout
        [Export] public string MapName { get; set; } = "";

        // on the board, not the fight: Godot readies children before parents, so the room is down before the fight exists
        // WHICH PLACE IS LAID OUT ON IT. This was Encounter: the board is somewhere now, and a
        // fight is one of the things that can happen there (PLACES_AND_PERSISTENCE.md section 1).
        [Export] public string Where { get; set; } = "";

        public const string MapsFolder = Content.Campaigns.Package.MapsFolder;

        public const string MapExtension = Content.Campaigns.Package.MapExtension;

        [Export] public float CellSize { get; set; } = 0.06f;

        [Export] public Material Mat { get; set; }

        [Export] public Material Lines { get; set; }

        [Export] public Material Wall { get; set; }

        [Export] public Material Door { get; set; }

        [Export] public Material Rough { get; set; }

        // left empty, the board falls back to the placeholder boxes above
        [Export] public PackedScene WallModel { get; set; }

        [Export] public PackedScene DoorwayModel { get; set; }

        [Export] public PackedScene RubbleModel { get; set; }

        [Export] public Material Paint { get; set; }

        [Export] public Color RefusedTint { get; set; } = new Color(0.62f, 0.18f, 0.14f, 0.5f);

        [Export] public NodePath PiecePath { get; set; } = "Mini";

        [Export] public string HeroId { get; set; } = EngineIds.Barbarian;

        // THE SHEET, where there is one (R0). A finished sheet REPLACES HeroId - it is the
        // character, and this board's hero is assembled from it rather than looked up by id.
        // Unset, or blank, and the board recruits exactly as it has since B4.
        [Export] public NodePath SheetPath { get; set; }

        // the board does not own a tray; empty means doors stay shut
        [Export] public NodePath TrayPath { get; set; }

        const float MatThickness = 0.006f;

        // a shade proud of the mat so the lines never z-fight
        const float LineWidth = 0.0022f;

        const float LineHeight = 0.0010f;

        const float LineRise = 0.0003f;

        BoardMetrics _metrics = BoardMetrics.Shipped;

        MapLayout _map;

        readonly Game.Campaigns.Library _archetypes = Game.Campaigns.Library.Load();

        Actor _hero;

        DiceTray _tray;

        Node3D _terrain;

        BoardTiles _tiles;

        // the door being approached or thrown at, a line not a square; null when nothing is pending
        Border? _door;

        // true while the door dice are in the air; clicks wait
        bool _checking;

        // public so a fight does not start a swing while a door check is in the air
        public bool IsChecking => _checking;

        DoorPiece _opened;

        Border? _opening;

        Cell? _wreckage;

        Grid<Mini> _grid;

        Mini _piece;

        CellFlash _refused;

        public BoardMetrics Metrics => _metrics;

        public MapLayout Map => _map;

        public Grid<Mini> Squares => _grid;

        // one hero Actor shared with the fight, so a door forced the hard way carries into the room
        public Mini Piece => _piece;

        public Actor Hero => _hero;

        // a delegate not an event: two answers to who decides a click would move the same piece twice
        public Func<Cell, bool> Claims { get; set; }

        public override void _Ready()
        {
            _map = Load();

            // metrics, grid and terrain all built from the map, so the three never disagree
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

        Actor Recruit()
        {
            if (FromTheSheet() is { } written) return written;

            if (!Fixed && Game.Campaigns.Requested.Hero.Length > 0)
                HeroId = Game.Campaigns.Requested.Hero;

            if (!Fixed && Game.Campaigns.Requested.Grown.Count > 0 && Grown.Count == 0)
                Grown = Game.Campaigns.Requested.Grown;

            if (_archetypes.Has(HeroId)) return Grow(_archetypes.Create(HeroId));

            GD.PushError($"board: there is no hero called '{HeroId}' in the roster " +
                         $"({string.Join(", ", _archetypes.Ids)}) - using {EngineIds.Barbarian}");

            return _archetypes.Create(EngineIds.Barbarian);
        }

        // R0. "The hero Actor is assembled FROM THE SHEET, replacing the hardcoded hero used
        // since B4." Null where there is no sheet or it has not been filled in, which is what a
        // cold boot and every check script look like - and then nothing about this board changes.
        Actor FromTheSheet()
        {
            if (SheetPath == null || SheetPath.IsEmpty) return null;

            var paper = GetNodeOrNull<Game.Sheet.Sheet>(SheetPath);

            if (paper == null || !paper.Finished) return null;

            return Assemble(paper.Character);
        }

        // exposed so the room can put a character on this board after boot - taking a box off the
        // shelf is loading a save, and what is in the box is a sheet
        public Actor Assemble(Content.Sheet.CharacterSheet sheet)
        {
            if (sheet == null || sheet.IsBlank) return null;

            foreach (Game.Campaigns.Loaded campaign in _archetypes.InPlay)
            {
                if (!campaign.Classes.Has(sheet.ClassId)) continue;

                Actor hero = sheet.Assemble(campaign.Classes, _archetypes.Items,
                                            SheetOptionsFor(sheet));

                GD.Print($"hero    assembled from the sheet - {sheet}");

                return hero;
            }

            GD.PushError($"board: the sheet says its class is '{sheet.ClassId}' and no such class " +
                         $"is installed - recruiting '{HeroId}' instead");

            return null;
        }

        // a race and a background may come from a different pack than the class
        Content.Sheet.SheetOptions SheetOptionsFor(Content.Sheet.CharacterSheet sheet)
        {
            foreach (Game.Campaigns.Loaded campaign in _archetypes.InPlay)
                if (campaign.Sheet.Has(sheet.RaceId) || campaign.Sheet.Has(sheet.BackgroundId))
                    return campaign.Sheet;

            return null;
        }

        public override void _ExitTree()
        {
            if (_piece != null) _piece.Arrived -= OnArrived;
            if (_tray != null) _tray.Resolved -= OnThrown;
        }

        // Godot's FileAccess, not System.IO: an exported map lives inside the .pck
        MapLayout Load()
        {
            string path = Chosen();

            string text = FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path) : null;

            if (text == null)
            {
                GD.PushError($"board: there is no map at '{path}' - " +
                             $"falling back to an empty room, {BoardMetrics.Shipped}");
                return EmptyRoom();
            }

            if (MapReader.TryRead(text, out MapLayout map, out string problem))
            {
                Loaded = path;
                return map;
            }

            GD.PushError($"board: {path} is not a map - {problem}. " +
                         $"falling back to an empty room, {BoardMetrics.Shipped}");

            return EmptyRoom();
        }

        public string Loaded { get; private set; }

        // Fixed opts a board out of --campaign, so a multi-fight run does not replace every board's room
        public bool Fixed { get; set; }

        // held here, not on the Actor: core knows no classes; the save writes step ids and the dice are re-derived
        public IReadOnlyList<string> Grown { get; set; } = System.Array.Empty<string>();

        // applied through the trait pipeline, so steps and Conditions compose in any order; an unknown step is skipped, not fatal
        Actor Grow(Actor hero)
        {
            if (Grown.Count == 0) return hero;

            Content.Classes.ClassCard card = _archetypes.ClassOf(hero?.Id);

            if (card == null)
            {
                GD.PushWarning($"board: '{hero?.Id}' is not a class, so it has no growth steps " +
                               "to be given");
                return hero;
            }

            foreach (string id in Grown)
            {
                Content.Classes.Growth step = card.Step(id);

                if (step == null)
                {
                    GD.PushWarning($"board: '{card.Id}' offers no growth step called '{id}'");
                    continue;
                }

                step.ApplyTo(hero);

                GD.Print($"grown   {hero.Id} - {step}");
            }

            return hero;
        }

        // the encounter's roster, read here because the room is chosen here; the fight musters from it
        public Content.Places.Place Plan { get; private set; }

        string Chosen()
        {
            // --campaign and --encounter beat the scene, so a second campaign plays without editing table.tscn
            if (!Fixed)
            {
                if (Game.Campaigns.Requested.Campaign.Length > 0)
                    Campaign = Game.Campaigns.Requested.Campaign;

                if (Game.Campaigns.Requested.Place.Length > 0)
                    Where = Game.Campaigns.Requested.Place;
            }

            if (string.IsNullOrWhiteSpace(Campaign)) return MapPath;

            string map = Named();

            if (map == null) return MapPath;

            foreach (string root in Game.Campaigns.CampaignFolders.Roots())
            {
                string path = System.IO.Path.Combine(root, Campaign, MapsFolder, map + MapExtension);

                if (System.IO.File.Exists(path)) return path;
            }

            GD.PushError($"board: no campaign on disk has {Campaign}/{MapsFolder}/{map}{MapExtension} - " +
                         $"falling back to {MapPath}");

            return MapPath;
        }

        // the full Library.Load is the one that validates the campaign, so a broken manifest is caught here
        string Named()
        {
            if (string.IsNullOrWhiteSpace(Where))
                return string.IsNullOrWhiteSpace(MapName) ? null : MapName;

            Game.Campaigns.Loaded campaign = Game.Campaigns.Library.Load(quiet: true).Campaign(Campaign);

            if (campaign == null || campaign.Failed)
            {
                GD.PushError($"board: '{Campaign}' " +
                             (campaign == null ? "is not installed" : "failed to load") +
                             $" - falling back to {MapPath}");
                return null;
            }

            Plan = campaign.Places.Of(Where);

            if (Plan == null)
            {
                GD.PushError($"board: '{Campaign}' has no place called '{Where}' - it has " +
                             $"{string.Join(", ", campaign.Places.Ids)}. " +
                             $"Falling back to {MapPath}");
                return null;
            }

            return Plan.Map;
        }

        // not a playable map: what the board shows while you read the error above
        static MapLayout EmptyRoom()
        {
            int columns = BoardMetrics.Shipped.Columns;
            int rows = BoardMetrics.Shipped.Rows;

            var tiles = new Tile[columns * rows];
            var vertical = new Edge[(columns + 1) * rows];
            var horizontal = new Edge[columns * (rows + 1)];

            for (int i = 0; i < tiles.Length; i++) tiles[i] = Tile.Floor;

            // walled on the outside lines, not solid squares, so every inside square is floor
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

        void Build()
        {
            var under = new Node3D { Name = "Surface" };
            AddChild(under);

            if (Mat == null || Lines == null)
                GD.PushError("board: the mat or the grid lines have no material - the board will be untextured");

            under.AddChild(new MeshInstance3D
            {
                Name = "Mat",
                Mesh = new BoxMesh { Size = new Vector3(_metrics.Width, MatThickness, _metrics.Depth) },
                MaterialOverride = Mat,
                Position = new Vector3(0f, -MatThickness * 0.5f, 0f),
            });

            var downwards = new BoxMesh { Size = new Vector3(LineWidth, LineHeight, _metrics.Depth + LineWidth) };
            var across = new BoxMesh { Size = new Vector3(_metrics.Width + LineWidth, LineHeight, LineWidth) };

            // one more line than squares: the outside edges are painted too
            for (int x = 0; x <= _metrics.Columns; x++)
                under.AddChild(Line($"Down{x:00}", downwards,
                    new Vector3(x * _metrics.CellSize - _metrics.HalfWidth, LineRise, 0f)));

            for (int y = 0; y <= _metrics.Rows; y++)
                under.AddChild(Line($"Across{y:00}", across,
                    new Vector3(0f, LineRise, y * _metrics.CellSize - _metrics.HalfDepth)));

            _terrain = new Node3D { Name = "Terrain" };
            AddChild(_terrain);

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

        // a developer affordance to see both check outcomes in one sitting, not a game rule
        const Key KeyToRelock = Key.R;

        // a click walks the piece anywhere reachable in one move; a shut door is the exception
        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: KeyToRelock })
            {
                Relock();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!@event.IsActionPressed("place_piece") || _piece == null) return;

            // busy: a move now would be a piece walking away from its own check
            if (_checking) return;

            Cell? clicked = CellUnder(@event, out Vector3 local);

            if (clicked == null) return;

            GetViewport().SetInputAsHandled();

            Cell cell = clicked.Value;

            // a fight decides what a square means; the board answers which square and stops
            if (Claims != null && Claims(cell)) return;

            Cell? standing = _grid.CellOf(_piece);

            if (standing == null) return;

            _door = null;

            Border? door = DoorNear(local) ?? ShutDoorBeside(cell, standing.Value);

            if (door != null)
            {
                Approach(door.Value, standing.Value);
                return;
            }

            if (standing == cell) return;

            IReadOnlyList<Cell> route =
                Route.Between(_map, standing.Value, cell, taken => _grid.IsOccupied(taken));

            if (route == null)
            {
                Refuse(cell, standing.Value);
                return;
            }

            // the grid moves first, the view catches up, so an interrupted move never leaves them disagreeing
            if (!_grid.Move(_piece, cell))
            {
                Refuse(cell, standing.Value);
                return;
            }

            _piece.Follow(Waypoints(route));

            GD.Print($"move    {standing} to {cell}, {route.Count - 1} squares, " +
                     $"{Route.Cost(_map, route)} to walk" +
                     (Sight.Clear(_map, standing.Value, cell) ? "" : " - out of sight, round a corner"));
        }

        // the board owns occupancy; null when the square is off the board, solid, or taken
        public Mini Place(PackedScene piece, Cell cell, string name)
        {
            if (piece == null) return null;

            if (!Free(cell, name)) return null;

            var mini = piece.Instantiate<Mini>();

            if (mini == null)
            {
                GD.PushError($"board: '{name}' is not a Mini scene - nothing placed on {cell}");
                return null;
            }

            mini.Name = name;

            return Stood(mini, cell);
        }

        // same placement for a pre-built mini from MiniMaker, not a second path
        public Mini Place(Mini piece, Cell cell, string name)
        {
            if (piece == null) return null;

            if (!Free(cell, name)) { piece.QueueFree(); return null; }

            piece.Name = name;

            return Stood(piece, cell);
        }

        Mini Stood(Mini mini, Cell cell)
        {
            AddChild(mini);

            _grid.Place(mini, cell);
            mini.PlaceAt(_metrics.Centre(cell));

            return mini;
        }

        // the three refusals asked once, so the two Place overloads cannot drift apart
        bool Free(Cell cell, string name)
        {
            if (_grid.Contains(cell) && _map.IsPassable(cell) && !_grid.IsOccupied(cell)) return true;

            GD.PushError($"board: nothing can stand on {cell} - " +
                         (!_grid.Contains(cell) ? "it is not a square on this board"
                          : !_map.IsPassable(cell) ? _map.At(cell).ToString()
                          : "somebody is already there") +
                         $", so '{name}' is not on the board");

            return false;
        }

        // no walk, no route: only a save loads this way, because a played piece never teleports
        public bool Stand(Mini piece, Cell cell)
        {
            if (piece == null) return false;

            Cell? standing = _grid.CellOf(piece);

            if (standing == cell) return true;

            if (!_grid.Contains(cell) || !_map.IsPassable(cell) || _grid.IsOccupied(cell))
                return false;

            if (standing != null) _grid.Remove(piece);

            _grid.Place(piece, cell);
            piece.PlaceAt(_metrics.Centre(cell));

            return true;
        }

        public void Lift(Mini piece)
        {
            if (piece == null) return;

            _grid.Remove(piece);
            piece.QueueFree();
        }

        public Cell? CellOf(Mini piece) => _grid.CellOf(piece);

        // same rules as a click walks the hero; false means no way there, already shown on the board
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

        // foul a square to Rough, double to cross; false when it was not plain floor, which is not a failure
        public bool Foul(Cell cell)
        {
            if (_map == null || _map.At(cell) != Tile.Floor) return false;

            _map = _map.With(cell, Tile.Rough);
            _tiles.Update(_terrain, _map, cell);

            return true;
        }

        // shove one square using the same Walk carry; a wall, edge or occupant just stops it
        public bool Shove(Mini piece, Cell from)
        {
            Cell? standing = _grid.CellOf(piece);

            if (_map == null || standing == null) return false;

            Cell to = new Cell(standing.Value.X + (standing.Value.X - from.X),
                               standing.Value.Y + (standing.Value.Y - from.Y));

            if (!_map.At(to).IsPassable()) return false;

            // a wall between them stops a shove, as it stops a step
            if (!_map.CanCross(standing.Value, to)) return false;

            if (_grid.At(to) != null) return false;

            return Walk(piece, to);
        }

        public void Refuse(Cell cell, Cell standing) => Refuse(_piece, cell, standing);

        public void Refuse(Mini piece, Cell cell, Cell standing)
        {
            _refused?.Show(_metrics.Centre(cell));
            piece?.Refuse(_metrics.Centre(cell));

            string because = !_map.IsPassable(cell) ? _map.At(cell).ToString()
                : _grid.IsOccupied(cell) ? "taken"
                : "walled off from where the piece is standing";

            GD.Print($"board   no way to {cell} from {standing} - {because}");
        }

        // a door is a line: found by distance, within half a square of the line it is drawn on
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

        // the forgiving version: a click beyond a shut door counts as clicking the door
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

        static IEnumerable<Border> Round(Cell cell)
        {
            yield return Border.North(cell);
            yield return Border.East(cell);
            yield return Border.South(cell);
            yield return Border.West(cell);
        }

        // walk up to the door, then throw; both squares beside it are floor, so either counts
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
                Refuse(door.Cell, standing);
                return;
            }

            Cell arriving = route[route.Count - 1];

            _door = door;
            _grid.Move(_piece, arriving);
            _piece.Follow(Waypoints(route));

            GD.Print($"move    {standing} to {arriving}, up to the door on {door}");
        }

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

            // set the readout to the door's own difficulty; a fight swing may have left it on a foe's Defense
            _tray.TargetDifficulty = DoorCheck.Against;

            // no second throwing path and no board dice: the tray throws the hero's own pool
            _tray.Throw(pool);
        }

        // the tray also throws for itself; act only on an answer this board asked for
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
            Cell? standing = _grid.CellOf(_piece);
            Cell beyond = standing == door.Cell ? door.Across : door.Cell;

            // renamed off the line so it can be redrawn as a gap without removing the door mid-swing
            _opened = _terrain?.GetNodeOrNull<DoorPiece>(BoardTiles.NameFor(door));

            if (_opened != null)
            {
                _opened.Name = "OpenedDoor";
                _opened.Open(outcome.Forced);
            }

            // the line opens either way: failure is content, not a wall
            _map = _map.With(door, Edge.None);
            _tiles.Update(_terrain, _map, door);

            _opening = door;
            _wreckage = null;

            // wreckage lands beyond the line; when the door was a square there was nowhere for it to go
            if (outcome.Leaves == Tile.Rough && _map.At(beyond) == Tile.Floor)
            {
                _map = _map.With(beyond, Tile.Rough);
                _tiles.Update(_terrain, _map, beyond);
                _wreckage = beyond;
            }

            // cost hits the hero's Vigor and Conditions, so a Winded hero throws smaller next time
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

        // developer affordance: put the door back and the hero whole
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

            // don't replace the hero mid-fight, or the encounter swings for an Actor nobody can see
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

        // met with the board's own plane, not a collider: exact, no physics; off the mat is refused, not clamped
        Cell? CellUnder(InputEvent @event, out Vector3 local)
        {
            local = Vector3.Zero;

            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return null;

            Vector2 at = @event is InputEventMouse mouse ? mouse.Position : GetViewport().GetMousePosition();

            var surface = new Plane(GlobalTransform.Basis.Y.Normalized(), GlobalTransform.Origin);

            Vector3? met = surface.IntersectsRay(camera.ProjectRayOrigin(at), camera.ProjectRayNormal(at));

            if (met == null) return null;

            // the point comes back too, not just the square, so a clicked door line can be found
            local = ToLocal(met.Value);

            Cell cell = _metrics.At(local);

            return _grid.Contains(cell) ? cell : null;
        }
    }
}

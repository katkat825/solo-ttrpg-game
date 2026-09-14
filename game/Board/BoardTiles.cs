using Godot;
using Core.Space;

namespace Game.Board
{
    // what the map looks like on the table: terrain standing on the mat wherever the map says
    // there is something.
    //
    // FLOOR IS NOTHING. The mat and its painted grid are the floor - a wet-erase battle map with
    // the room drawn on it - so a floor square gets no mesh at all, and what stands on top is the
    // terrain: rock in a square, rubble in a square, and walls and doors ON THE LINES between
    // squares. That is how a real table does it, and it is also the cheapest thing that could
    // work: an 80-square room is a few dozen pieces.
    //
    // THE LINES ARE WHY THIS FILE GOT SIMPLER (EDGE_WALLS.md). A wall used to be a square, and
    // drawing one meant asking every neighbour which of its four faces to put a panel on - the
    // fiddliest code on the board, and still wrong: a one-square-thick wall came out as two panels
    // with a square's width of nothing between them, which reads as hollow because it IS hollow.
    // Now a wall is an edge, and an edge is already a position and an orientation. One panel, on
    // the line, facing the way the line runs. Nothing is inferred.
    //
    // BUILT FROM THE MAP, EVERY TIME. Nothing here is authored in a scene, because a hand-placed
    // wall would be a second statement of something the map file already says, and the two would
    // disagree the first time the file was edited - which is the exact failure B2 exists to remove.
    // Update redraws one square or one line, so a door forced during play is redrawn from the map
    // rather than from anybody's memory of what used to be there.
    //
    // B5 PUT REAL TERRAIN ON IT. The models come from the KayKit dungeon pack through
    // tools/pull-models.ps1 and wear the game's palette and the painted-miniature shader; they are
    // handed in from board.tscn, and WHERE THERE IS NO MODEL THE BOXES ARE STILL HERE. That is not
    // timidity: a box that is the right size in the right place is how every square and line on
    // this board was verified, and a milestone that swaps the art should not be able to take the
    // geometry with it. Clear the model exports and the board is a board of blocks again.
    public sealed class BoardTiles
    {
        // ---- the placeholder boxes, in metres ----

        // walls stand a little lower than the mini is tall, so a piece is never hidden behind one
        // from the table's own 60 degrees. cover you cannot see over is a Phase C decision and it
        // will be made with a camera to look through
        public const float WallHeight = 0.048f;

        // how thick a wall drawn on a line is. a real partition is a hand's width; this is the
        // fraction of a square it eats, which is what keeps the squares either side of it usable
        public const float WallThickness = 0.2f;

        // a door is the same line, lower, so the two are told apart by silhouette at the table's
        // own angle rather than by colour
        public const float DoorHeight = 0.036f;

        // rubble is a patch on the mat rather than an object on it
        public const float RoughHeight = 0.0035f;

        // and it stops short of the grid line, so the square it makes difficult is still legible
        // as one square
        public const float RoughInset = 0.86f;

        // rock fills its square - it is the other kind of barrier, a mass rather than a partition,
        // and the whole point of it is that there is no standing on either side of it
        public const float RockHeight = 0.055f;

        // ---- the models ----

        // how much of a square a pile of rubble covers
        public const float RubbleSpread = 0.92f;

        readonly BoardMetrics _metrics;

        public BoardTiles(BoardMetrics metrics) => _metrics = metrics;

        public Material Wall { get; set; }

        public Material Door { get; set; }

        public Material Rough { get; set; }

        // the painted models, or null for the boxes above
        public PackedScene WallModel { get; set; }

        public PackedScene DoorwayModel { get; set; }

        public PackedScene RubbleModel { get; set; }

        // one shader over all of them - the whole pack shares one atlas, so it is one material
        public Material Paint { get; set; }

        public void Build(Node3D under, MapLayout map)
        {
            if (under == null || map == null) return;

            if (Wall == null || Door == null || Rough == null)
                GD.PushError("board: a tile kind has no material - that terrain will be untextured");

            foreach (Cell at in map.Cells) Raise(under, map, at);

            foreach (Border on in map.Borders) Raise(under, map, on);
        }

        // one square, redrawn: whatever was standing there goes, and whatever the map says now
        // takes its place. the caller is expected to have changed the map first
        public void Update(Node3D under, MapLayout map, Cell at)
        {
            if (under == null || map == null) return;

            Clear(under, NameFor(at));
            Raise(under, map, at);
        }

        // and one line, which is how a door opens
        public void Update(Node3D under, MapLayout map, Border on)
        {
            if (under == null || map == null) return;

            Clear(under, NameFor(on));
            Raise(under, map, on);
        }

        // takes a piece off the board, if there is one there
        public void Clear(Node3D under, string named)
        {
            Node standing = under?.GetNodeOrNull(named);

            if (standing == null) return;

            // detached first so a rebuild in the same frame cannot find it again
            under.RemoveChild(standing);
            standing.QueueFree();
        }

        // every piece is named for the square or the line it stands on and nothing else, so finding
        // one needs no memory of what kind it was - which is what lets a door become a gap
        public static string NameFor(Cell at) => $"Tile{at.X:00}x{at.Y:00}";

        public static string NameFor(Border on) =>
            $"Line{on.Cell.X:00}x{on.Cell.Y:00}{(on.Vertical ? "V" : "H")}";

        void Raise(Node3D under, MapLayout map, Cell at)
        {
            Node3D piece = map.At(at) switch
            {
                Tile.Void => WallModel != null ? Rock(at) : Block(at, RockHeight, _metrics.CellSize, Wall),
                Tile.Rough => RubbleModel != null ? Rubble(at) : Block(at, RoughHeight,
                                                                      _metrics.CellSize * RoughInset, Rough),
                _ => null,
            };

            if (piece != null) under.AddChild(piece);
        }

        void Raise(Node3D under, MapLayout map, Border on)
        {
            Node3D piece = map.At(on) switch
            {
                Edge.Wall => WallModel != null ? Panel(on) : Slab(on, WallHeight, Wall),
                Edge.Door => DoorwayModel != null ? Doorway(on) : Hang(on),
                _ => null,
            };

            if (piece != null) under.AddChild(piece);
        }

        // ---- where a line is ----

        // where a line is on the table - BoardMetrics answers it, because "where is that" is the
        // one question it exists to answer and the board asks the same one when a door is clicked
        Vector3 Centre(Border on) => _metrics.Centre(on);

        // the model runs east-west, which is what a line along a row does; a line up a column is
        // the same piece turned a quarter
        static float Facing(Border on) => on.Vertical ? Mathf.Pi * 0.5f : 0f;

        // ---- the models ----

        Node3D Panel(Border on)
        {
            Node3D panel = Painted(WallModel, NameFor(on));
            Aabb bounds = PaintedModel.Bounds(panel);

            float across = PaintedModel.ToFitWidth(bounds, _metrics.CellSize);

            // UNIFORM, at the pack's own proportions. a wall on a line is exactly what a modular
            // dungeon kit models - 4 units along, 1 deep - so nothing has to be stretched to fit,
            // and the panels of a run meet the ones perpendicular to them at the corner they share
            panel.Scale = Vector3.One * across;
            panel.Rotation = new Vector3(0f, Facing(on), 0f);
            panel.Position = Centre(on) - new Vector3(0f, bounds.Position.Y * across, 0f);

            return panel;
        }

        Node3D Rock(Cell at)
        {
            Node3D rock = Painted(WallModel, NameFor(at));
            Aabb bounds = PaintedModel.Bounds(rock);

            float across = PaintedModel.ToFitWidth(bounds, _metrics.CellSize);

            // the one place a wall model IS stretched, and it is the right thing here: rock is a
            // mass filling its square rather than a partition standing on a line, so the panel's
            // depth is pulled out to the square's width. nothing rotates, so nothing shears
            float deep = bounds.Size.Z > 0f ? _metrics.CellSize / bounds.Size.Z : across;

            rock.Scale = new Vector3(across, across, deep);
            rock.Position = _metrics.Centre(at) - new Vector3(0f, bounds.Position.Y * across, 0f);

            return rock;
        }

        Node3D Rubble(Cell at)
        {
            Node3D rubble = Painted(RubbleModel, NameFor(at));
            Aabb bounds = PaintedModel.Bounds(rubble);

            float scale = PaintedModel.ToFitWidth(bounds, _metrics.CellSize * RubbleSpread);

            rubble.Scale = Vector3.One * scale;
            rubble.Position = _metrics.Centre(at) - new Vector3(0f, bounds.Position.Y * scale, 0f);

            // every pile of rubble the same way round is wallpaper; one that turns when you reload
            // is a bug. the angle comes out of the square's own coordinates
            rubble.Rotation = new Vector3(0f, PaintedModel.SettledAngle(at.X, at.Y), 0f);

            return rubble;
        }

        // a doorway is a wall with a hole in it AND a door hanging in the hole, in one model. the
        // leaf is lifted out onto a hinge of its own so it can swing while the frame stays put
        Node3D Doorway(Border on)
        {
            Node3D model = Painted(DoorwayModel, "Frame");
            Aabb bounds = PaintedModel.Bounds(model);

            float scale = PaintedModel.ToFitWidth(bounds, _metrics.CellSize);

            var piece = new DoorPiece
            {
                Name = NameFor(on),
                Position = Centre(on) - new Vector3(0f, bounds.Position.Y * scale, 0f),
                Scale = Vector3.One * scale,
                Rotation = new Vector3(0f, Facing(on), 0f),
            };

            piece.AddChild(model);

            Node3D leaf = FindLeaf(model);

            if (leaf == null)
            {
                GD.PushError($"board: {DoorwayModel.ResourcePath} has no door in it - the doorway will not open");
                return piece;
            }

            // the hinge is the leaf's own outside edge, in the model's units, and the leaf is
            // moved back by the same amount so it does not shift when it is reparented
            Aabb leafBounds = leaf.Transform * PaintedModel.Bounds(leaf);
            var hinge = new Node3D { Name = "Hinge", Position = new Vector3(leafBounds.Position.X, 0f, 0f) };

            Transform3D was = leaf.Transform;

            // the model came out of a PackedScene, so every node in it is owned by the root. a
            // child of the root that is reparented while still owned by it is a scene that cannot
            // be saved, which Godot says out loud - the leaf belongs to the board now
            leaf.Owner = null;
            leaf.GetParent().RemoveChild(leaf);
            hinge.AddChild(leaf);
            leaf.Transform = new Transform3D(was.Basis, was.Origin - hinge.Position);

            piece.AddChild(hinge);
            piece.Leaf = hinge;
            piece.Drop = -leafBounds.Size.Y * 0.1f;

            return piece;
        }

        Node3D Painted(PackedScene model, string name)
        {
            var instance = model.Instantiate<Node3D>();

            instance.Name = name;
            PaintedModel.Paint(instance, Paint);

            return instance;
        }

        // the door panel inside a doorway model, by name. KayKit calls it <model>_door and nothing
        // else in the piece is called anything like it
        static Node3D FindLeaf(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is Node3D found && child.Name.ToString().ToLower().EndsWith("_door")) return found;

                Node3D deeper = FindLeaf(child);

                if (deeper != null) return deeper;
            }

            return null;
        }

        // ---- the placeholder boxes ----

        // standing ON the mat: the box sits on the surface rather than in it, which is why the
        // height is halved into the offset and never subtracted from it
        MeshInstance3D Block(Cell at, float height, float across, Material material) => new MeshInstance3D
        {
            Name = NameFor(at),
            Mesh = new BoxMesh { Size = new Vector3(across, height, across) },
            MaterialOverride = material,
            Position = _metrics.Centre(at) + new Vector3(0f, height * 0.5f, 0f),
        };

        // a wall as a box on a line: as long as the square it runs beside, and thin
        MeshInstance3D Slab(Border on, float height, Material material)
        {
            float cell = _metrics.CellSize;
            float thick = cell * WallThickness;

            return new MeshInstance3D
            {
                Name = NameFor(on),
                Mesh = new BoxMesh
                {
                    Size = on.Vertical
                        ? new Vector3(thick, height, cell)
                        : new Vector3(cell, height, thick),
                },
                MaterialOverride = material,
                Position = Centre(on) + new Vector3(0f, height * 0.5f, 0f),
            };
        }

        // and a door as one, hung on a hinge at one end of the line so it can swing
        DoorPiece Hang(Border on)
        {
            float cell = _metrics.CellSize;
            float thick = cell * WallThickness;
            float half = cell * 0.47f;

            var door = new DoorPiece
            {
                Name = NameFor(on),
                Position = Centre(on),
                Drop = -DoorHeight * 0.15f,
            };

            // the hinge sits at one end of the line; the panel hangs across to the other
            Vector3 hinge = on.Vertical ? new Vector3(0f, 0f, -half) : new Vector3(-half, 0f, 0f);

            var leaf = new Node3D { Name = "Hinge", Position = hinge };

            leaf.AddChild(new MeshInstance3D
            {
                Name = "Panel",
                Mesh = new BoxMesh
                {
                    Size = on.Vertical
                        ? new Vector3(thick, DoorHeight, cell * 0.94f)
                        : new Vector3(cell * 0.94f, DoorHeight, thick),
                },
                MaterialOverride = Door,
                Position = new Vector3(-hinge.X, DoorHeight * 0.5f, -hinge.Z),
            });

            door.AddChild(leaf);
            door.Leaf = leaf;

            return door;
        }
    }
}

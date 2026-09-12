using Godot;
using Core.Space;

namespace Game.Board
{
    // what the map looks like on the table: a piece of terrain standing on the mat for every
    // square that is not plain floor.
    //
    // FLOOR IS NOTHING. The mat and its painted grid are the floor - a wet-erase battle map with
    // the room drawn on it - so a floor square gets no mesh at all, and what stands on top is the
    // terrain: wall blocks, a shut door, a patch of rubble. That is how a real table does it, and
    // it is also the cheapest thing that could work: an 80-square room is a couple of dozen boxes.
    //
    // BUILT FROM THE MAP, EVERY TIME. Nothing here is authored in a scene, because a hand-placed
    // wall would be a second statement of something the map file already says, and the two would
    // disagree the first time the file was edited - which is the exact failure B2 exists to remove.
    //
    // Placeholder geometry, deliberately, the way the d6 was a BoxMesh and the mini still is a
    // capsule. B5 pulls real tiles out of the KayKit dungeon pack and puts the painted-miniature
    // shader on them; the only thing that changes here is which mesh each kind gets.
    public sealed class BoardTiles
    {
        // walls stand a little lower than the mini is tall, so a piece is never hidden behind one
        // from the table's own 60 degrees. cover you cannot see over is a Phase C decision and it
        // will be made with a camera to look through
        public const float WallHeight = 0.048f;

        // a shut door reads as a door by being LOWER and warmer than the wall it sits in - the
        // silhouette is what tells them apart at this angle, not the colour
        public const float DoorHeight = 0.026f;

        // rubble is a patch on the mat rather than an object on it
        public const float RoughHeight = 0.0035f;

        // and it stops short of the grid line, so the square it makes difficult is still legible
        // as one square
        public const float RoughInset = 0.86f;

        // a door is set into the wall line rather than filling the doorway edge to edge
        public const float DoorInset = 0.9f;

        readonly BoardMetrics _metrics;

        public BoardTiles(BoardMetrics metrics) => _metrics = metrics;

        public Material Wall { get; set; }

        public Material Door { get; set; }

        public Material Rough { get; set; }

        // one mesh per kind, shared by every instance of it - the squares are all the same size,
        // so there is nothing per-square to build
        public void Build(Node3D under, MapLayout map)
        {
            if (under == null || map == null) return;

            if (Wall == null || Door == null || Rough == null)
                GD.PushError("board: a tile kind has no material - that terrain will be untextured");

            float cell = _metrics.CellSize;

            // walls fill their square edge to edge, so a run of them is one wall rather than a row
            // of blocks with hairlines between
            var wall = new BoxMesh { Size = new Vector3(cell, WallHeight, cell) };
            var door = new BoxMesh { Size = new Vector3(cell * DoorInset, DoorHeight, cell * DoorInset) };
            var rough = new BoxMesh { Size = new Vector3(cell * RoughInset, RoughHeight, cell * RoughInset) };

            foreach (Cell at in map.Cells)
            {
                Tile tile = map.At(at);

                MeshInstance3D piece = tile switch
                {
                    Tile.Wall => Stand(at, wall, Wall, WallHeight, "Wall"),
                    Tile.Door => Stand(at, door, Door, DoorHeight, "Door"),
                    Tile.Rough => Stand(at, rough, Rough, RoughHeight, "Rough"),
                    _ => null,
                };

                if (piece != null) under.AddChild(piece);
            }
        }

        // standing ON the mat: the box sits on the surface rather than in it, which is why the
        // height is halved into the offset and never subtracted from it
        MeshInstance3D Stand(Cell at, Mesh mesh, Material material, float height, string kind) =>
            new MeshInstance3D
            {
                // named for the square it is on - the remote scene tree is the only place anyone
                // ever looks for one of these, and "Wall" forty times over says nothing
                Name = $"{kind}{at.X:00}x{at.Y:00}",
                Mesh = mesh,
                MaterialOverride = material,
                Position = _metrics.Centre(at) + new Vector3(0f, height * 0.5f, 0f),
            };
    }
}

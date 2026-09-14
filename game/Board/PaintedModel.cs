using System.Collections.Generic;
using Godot;

namespace Game.Board
{
    // a model out of an asset pack, made into a piece of this game: measured, sized to the board,
    // and painted.
    //
    // THE THIRD STEP OF THE PIPELINE (THE_BOARD.md B5), and the only part of it that runs in the
    // engine. tools/pull-models.ps1 culls a pack down to what ships and tools/bake-palette.ps1
    // recolours its atlas; this is what puts the painted-miniature shader on the result and fits
    // it to a 60 mm square.
    //
    // WHY THE MATERIAL IS OVERRIDDEN RATHER THAN IMPORTED. A .gltf brings its own
    // StandardMaterial3D per mesh, drawn by somebody else, and that is exactly what makes nine CC0
    // packs look like nine CC0 packs. Every mesh in the game wears one shader instead. The
    // imported materials are left alone rather than edited, so re-importing a pack cannot undo it.
    //
    // WHY IT MEASURES RATHER THAN ASSUMING. KayKit's dungeon tiles are 4 units wide and its heroes
    // are 2.4 units tall; Quaternius uses different numbers again, and nothing says either of them
    // in a file this project owns. Asking the mesh how big it is means a pack can be swapped for
    // another without a single constant changing.
    public static class PaintedModel
    {
        // every mesh under a node, including the node itself
        public static IEnumerable<MeshInstance3D> Meshes(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null) yield return mesh;

            foreach (Node child in node.GetChildren())
                foreach (MeshInstance3D deeper in Meshes(child))
                    yield return deeper;
        }

        // one shader over everything under here
        public static void Paint(Node node, Material paint)
        {
            if (node == null || paint == null) return;

            foreach (MeshInstance3D mesh in Meshes(node)) mesh.MaterialOverride = paint;
        }

        // how big this model is, in its own units, with every child's transform taken into account
        public static Aabb Bounds(Node3D node)
        {
            Aabb whole = default;
            bool any = false;

            foreach (MeshInstance3D mesh in Meshes(node))
            {
                // relative to the node being asked about rather than to the world, so this can be
                // asked before the model is standing anywhere
                Aabb box = Relative(node, mesh) * mesh.GetAabb();

                whole = any ? whole.Merge(box) : box;
                any = true;
            }

            return whole;
        }

        static Transform3D Relative(Node3D root, Node3D node)
        {
            var transform = Transform3D.Identity;

            for (Node3D at = node; at != null && at != root; at = at.GetParent() as Node3D)
                transform = at.Transform * transform;

            return transform;
        }

        // the scale that makes a model this many metres across its widest horizontal dimension.
        // horizontal, because a square is what a piece has to fit in - a tall model is allowed to
        // be tall, and a mini that is shrunk until its axe fits inside its square is a doll
        public static float ToFitWidth(Aabb bounds, float metres)
        {
            float widest = Mathf.Max(bounds.Size.X, bounds.Size.Z);

            return widest <= 0f ? 1f : metres / widest;
        }

        // and the one that makes it this tall
        public static float ToFitHeight(Aabb bounds, float metres) =>
            bounds.Size.Y <= 0f ? 1f : metres / bounds.Size.Y;

        // a yaw that is the same every time this square is drawn and different from its
        // neighbours'. rubble laid out in rows all facing the same way reads as wallpaper; rubble
        // that spins when you reload reads as a bug
        public static float SettledAngle(int x, int y)
        {
            int hash = (x * 73856093) ^ (y * 19349663);

            return (hash & 0x3FF) / 1024f * Mathf.Tau;
        }
    }
}

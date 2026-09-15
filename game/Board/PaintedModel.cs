using System.Collections.Generic;
using Godot;

namespace Game.Board
{
    public static class PaintedModel
    {
        public static IEnumerable<MeshInstance3D> Meshes(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null) yield return mesh;

            foreach (Node child in node.GetChildren())
                foreach (MeshInstance3D deeper in Meshes(child))
                    yield return deeper;
        }

        public static void Paint(Node node, Material paint)
        {
            if (node == null || paint == null) return;

            foreach (MeshInstance3D mesh in Meshes(node)) mesh.MaterialOverride = paint;
        }

        public static Aabb Bounds(Node3D node)
        {
            Aabb whole = default;
            bool any = false;

            foreach (MeshInstance3D mesh in Meshes(node))
            {
                // relative to the node, not the world, so bounds work before it is placed
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

        // fit the widest horizontal size; a tall model may stay tall
        public static float ToFitWidth(Aabb bounds, float metres)
        {
            float widest = Mathf.Max(bounds.Size.X, bounds.Size.Z);

            return widest <= 0f ? 1f : metres / widest;
        }

        public static float ToFitHeight(Aabb bounds, float metres) =>
            bounds.Size.Y <= 0f ? 1f : metres / bounds.Size.Y;

        // deterministic per-square yaw: same on reload, different from neighbours
        public static float SettledAngle(int x, int y)
        {
            int hash = (x * 73856093) ^ (y * 19349663);

            return (hash & 0x3FF) / 1024f * Mathf.Tau;
        }
    }
}

using Godot;

namespace Game.Tray
{
    // the render mesh for one tray wall: a bevelled, mitred rail
    //
    // RENDER ONLY. Nothing here touches a CollisionShape3D, so the physics the fairness sweep
    // measured is untouched - dice still bounce off the same box faces they always did, and no
    // re-sweep is owed for the three cosmetic changes this makes. DiceTray builds one of these per
    // wall from the wall's own collision box and hangs it on the existing MeshInstance3D, so the
    // skin's material still lands on it (DiceTray.Meshes walks it) and TrayBounds is unaffected.
    //
    // Three changes, all above the felt, none of them moving an inner face:
    //   - 45 degree mitred ends, so the corners read as a joined frame rather than four
    //     overlapping boxes. the outer edge of each rail runs the full length and the inner edge
    //     is a wall-thickness shorter at each end, the way a real mitre is cut
    //   - a noticeable chamfer on the OUTER top edge - the polished bevel a wooden tray has, and
    //     the safest change there is: that edge faces away from the dice and is never touched
    //   - a barely-there chamfer on the INNER top edge, because planed wood is never a true 90
    //
    // The rim height and the wall angle are deliberately NOT changed: those are physical and would
    // owe a sweep. See DICE_TRAY.md.
    //
    // Every triangle's normal AND winding are forced to point away from the rail's own centre, so
    // a mistake in the profile order cannot leave a face dark or culled - "outward" is computed,
    // not trusted. Generated at runtime, deliberately not a [Tool] script: baking geometry into
    // the scene is how a stale mesh and the real dimensions drift apart, the reason DieSolid
    // generates too.
    //
    // The two bevel sizes are constants, meant to be tuned by eye.
    public static class TrayRail
    {
        // the polished outer bevel - a chamfer on the top outer edge. noticeable on purpose
        public const float OuterBevel = 0.006f;

        // the inner top edge - just off square, so it catches the light like planed wood
        public const float InnerBevel = 0.0015f;

        // thickness, height and length are the wall's own dimensions, read off its collision box.
        // across/up/along place this rail into the wall's local frame: across is the outward
        // horizontal (+ = away from the tray centre), up is +Y, along is the wall's length. one
        // generator serves all four walls whichever way they run and whichever way they face.
        public static ArrayMesh Build(
            float thickness, float height, float length,
            Vector3 across, Vector3 up, Vector3 along)
        {
            float a = thickness * 0.5f;
            float top = height * 0.5f;
            float bottom = -height * 0.5f;
            float half = length * 0.5f;

            // a bevel can never eat past the rail it sits on
            float bevOut = Mathf.Min(OuterBevel, Mathf.Min(thickness * 0.9f, height * 0.5f));
            float bevIn = Mathf.Min(InnerBevel, Mathf.Min(thickness * 0.45f, height * 0.5f));

            // the cross-section, across (x) by up (y), walked once. +x is the outer face
            Vector2[] profile =
            {
                new(-a, bottom),          // inner bottom
                new(a, bottom),           // outer bottom
                new(a, top - bevOut),     // up the outer face to the polished bevel
                new(a - bevOut, top),     // outer top, past the bevel
                new(-a + bevIn, top),     // across the top to the small inner bevel
                new(-a, top - bevIn),     // inner top, past it
            };

            // the mitre: the outer edge (+x) runs the full length, the inner edge (-x) is one wall
            // thickness shorter at each end, so two perpendicular rails meet on a 45 degree line
            float EndHi(float x) => (half - a) + x;
            float EndLo(float x) => -(half - a) - x;

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            int n = profile.Length;

            // side faces - one quad per profile edge, spanning the mitred length
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = profile[i];
                Vector2 p1 = profile[(i + 1) % n];

                var loA = new Vector3(p0.X, p0.Y, EndLo(p0.X));
                var hiA = new Vector3(p0.X, p0.Y, EndHi(p0.X));
                var loB = new Vector3(p1.X, p1.Y, EndLo(p1.X));
                var hiB = new Vector3(p1.X, p1.Y, EndHi(p1.X));

                Tri(st, across, up, along, loA, hiA, hiB);
                Tri(st, across, up, along, loA, hiB, loB);
            }

            // the two mitred end caps, each the profile laid on its own 45 degree plane
            var hi = new Vector3[n];
            var lo = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = profile[i];
                hi[i] = new Vector3(p.X, p.Y, EndHi(p.X));
                lo[i] = new Vector3(p.X, p.Y, EndLo(p.X));
            }

            for (int i = 1; i < n - 1; i++)
            {
                Tri(st, across, up, along, hi[0], hi[i], hi[i + 1]);
                Tri(st, across, up, along, lo[0], lo[i], lo[i + 1]);
            }

            return st.Commit();
        }

        // canonical (across, up, length) coordinates into the wall's local frame
        static Vector3 Map(Vector3 across, Vector3 up, Vector3 along, Vector3 c) =>
            across * c.X + up * c.Y + along * c.Z;

        // one triangle, oriented so its front face and its normal both point away from the rail's
        // centre. the rail is built around its own origin, so "away from the centre" is just the
        // triangle centroid's own direction - which removes any dependence on getting the profile
        // wound correctly by hand, the one thing that cannot be checked without opening the editor
        static void Tri(SurfaceTool st, Vector3 across, Vector3 up, Vector3 along,
                        Vector3 ca, Vector3 cb, Vector3 cc)
        {
            Vector3 A = Map(across, up, along, ca);
            Vector3 B = Map(across, up, along, cb);
            Vector3 C = Map(across, up, along, cc);

            Vector3 normal = (B - A).Cross(C - A);
            if (normal.LengthSquared() <= 0f) return;   // a degenerate sliver, skip it
            normal = normal.Normalized();

            Vector3 centre = (A + B + C) / 3f;
            bool flip = normal.Dot(centre) < 0f;
            if (flip) normal = -normal;

            st.SetNormal(normal);

            // grain runs along the rail's length; v climbs its height
            if (flip)
            {
                Emit(st, A, ca);
                Emit(st, C, cc);
                Emit(st, B, cb);
            }
            else
            {
                Emit(st, A, ca);
                Emit(st, B, cb);
                Emit(st, C, cc);
            }
        }

        static void Emit(SurfaceTool st, Vector3 world, Vector3 canon)
        {
            st.SetUV(new Vector2(canon.Z, canon.Y));
            st.AddVertex(world);
        }
    }
}

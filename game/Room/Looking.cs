using Godot;

namespace Game.Room
{
    // HOW MUCH OF THE MAT YOU ARE LOOKING AT, AND WHICH PART.
    //
    // Two moves, and they are easy to conflate, so they are named apart here:
    //
    //   ZOOM  push in or pull back, so squares get bigger or smaller - see less of the mat in
    //         more detail, or all of it smaller
    //   PAN   at a fixed zoom, slide the window across a mat bigger than the table
    //
    // A single dungeon room fits the table at once and has nothing to pan. A town or a region is a
    // mat bigger than the table, so you move your head across it. Both are the same lean-in idea
    // and neither is a viewport widget.
    //
    // THE ONE RULE IT ENFORCES is that you can never look past the edge of the mat. Panning is
    // clamped to what the mat holds, and pulling back re-clamps - which is the case that bites,
    // because a window that was legal at one zoom hangs off the edge at a wider one.
    public sealed class Looking
    {
        // pulled all the way back is the whole mat; there is nothing wider to see
        public const float Widest = 1f;

        // close enough to read one square, and no closer - past this the mat is a texture
        public const float Closest = 4f;

        public Looking(Vector2 mat)
        {
            Mat = new Vector2(Mathf.Max(mat.X, 0f), Mathf.Max(mat.Y, 0f));
            Zoom = Widest;
            Pan = Vector2.Zero;
        }

        // the mat's whole extent, in metres
        public Vector2 Mat { get; }

        public float Zoom { get; private set; }

        // the middle of what you are looking at, measured from the middle of the mat
        public Vector2 Pan { get; private set; }

        // how much of the mat is in front of you
        public Vector2 Shown => Mat / Zoom;

        // how far the middle may move before the window hangs off the edge
        public Vector2 Limit => new Vector2(Mathf.Max((Mat.X - Shown.X) * 0.5f, 0f),
                                            Mathf.Max((Mat.Y - Shown.Y) * 0.5f, 0f));

        // a mat the table can show at once has nowhere to pan to, and saying so is what stops a
        // single room drifting under the hand
        public bool Fits => Limit.X <= 0f && Limit.Y <= 0f;

        public void ZoomTo(float zoom)
        {
            Zoom = Mathf.Clamp(zoom, Widest, Closest);

            // RE-CLAMPED, always. A pan that was inside the mat at four times is off the end of it
            // at one, so pulling back has to bring the window home with it
            PanTo(Pan);
        }

        public void ZoomBy(float by) => ZoomTo(Zoom + by);

        public void PanTo(Vector2 to)
        {
            Vector2 limit = Limit;

            Pan = new Vector2(Mathf.Clamp(to.X, -limit.X, limit.X),
                              Mathf.Clamp(to.Y, -limit.Y, limit.Y));
        }

        public void PanBy(Vector2 by) => PanTo(Pan + by);

        // back to the whole mat, square on
        public void Back()
        {
            Zoom = Widest;
            Pan = Vector2.Zero;
        }

        // where the eye sits over the mat, in the mat's own space: the middle of the window, and
        // how far up is the caller's - this owns the two moves and not the camera
        public Vector3 Middle => new Vector3(Pan.X, 0f, Pan.Y);

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"looking at {Shown.X:0.00} x {Shown.Y:0.00} of a {Mat.X:0.00} x {Mat.Y:0.00} mat " +
            $"at {Zoom:0.0}x, centred {Pan}" + (Fits ? " - it all fits" : "");
    }
}

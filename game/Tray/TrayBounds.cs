using Godot;

namespace Game.Tray
{
    // how big the tray is, in the tray's OWN space - the one place that answers it
    //
    // until F2 this was four constants in three files that never referenced each other:
    // DieBody.LostBelowY and LostRadius, DieMark.FeltNearEdge and FeltSideEdge. All four were
    // world coordinates measured from Vector3.Zero, so the tray could only ever sit at the world
    // origin, and all four were hand-copied from dice_tray.tscn rather than derived from it.
    // Resizing the floor moved none of them - the failure being a die reported as outside the
    // tray while visibly sitting on the felt. SEAMS.md 5 and 8.
    //
    // Everything here is derived from the floor box and the wall thickness, which DiceTray reads
    // off the scene. The scene is the one description of how big the tray is; this is what reads
    // it, and nothing downstream carries a copy.
    //
    // Godot-free apart from Vector2, so game.tests can hold the derivations to the tray M9
    // actually shipped. Pure: it knows the tray's shape and nothing about where the tray stands.
    public sealed class TrayBounds
    {
        // half the felt floor, along X and along Z
        public float HalfWidth { get; }

        public float HalfDepth { get; }

        // the felt surface itself - the top of the floor box, not its centre
        // marks are drawn a hair above this, dice come to rest on it
        public float FeltY { get; }

        // the walls stand on the floor's outer edge, so the felt a label can use is this much
        // narrower than the floor at each side
        public float WallThickness { get; }

        // beyond this multiple of the floor's half-diagonal, a die is gone rather than merely
        // near a wall. generous on purpose: escape detection ends a throw that would otherwise
        // hang forever, so a false positive is worse than a slow one, and a die wedged against
        // the outside of a wall is still inside this
        public const float LostMargin = 1.5f;

        // and this far below the felt, which no die on the table can reach
        public const float LostDrop = 0.2f;

        public TrayBounds(float halfWidth, float halfDepth, float feltY, float wallThickness)
        {
            HalfWidth = halfWidth;
            HalfDepth = halfDepth;
            FeltY = feltY;
            WallThickness = wallThickness;
        }

        // the felt as a label may use it - a name past these edges slides under the woodwork,
        // which looks exactly like a die that was never marked
        public float FeltSideEdge => HalfWidth - WallThickness;

        public float FeltNearEdge => HalfDepth - WallThickness;

        // corner to centre. the furthest a die can be while still on the felt, which is what
        // makes it the right thing to scale the escape radius from
        public float HalfDiagonal => new Vector2(HalfWidth, HalfDepth).Length();

        public float LostRadius => HalfDiagonal * LostMargin;

        public float LostBelowY => FeltY - LostDrop;

        // true when these numbers describe a tray a die could actually land in
        // a floor smaller than its own walls has no felt, and every edge below goes negative
        public bool IsUsable =>
            HalfWidth > WallThickness && HalfDepth > WallThickness && WallThickness >= 0f;

        // the tray dice_tray.tscn was authored with, and the fallback when the scene cannot be
        // measured. floor 0.64 x 0.49, walls 0.02 thick, felt at the tray's own zero
        //
        // this is a SECOND statement of the scene's numbers and the only one in the project.
        // TrayBoundsTests holds it to the values M9 shipped, so if the scene is resized and this
        // is not, the test says so - which is the arrangement PoolOdds uses for the Snag rate
        public static readonly TrayBounds Shipped = new(0.32f, 0.245f, 0f, 0.02f);

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            $"floor {HalfWidth * 2f:0.000} x {HalfDepth * 2f:0.000}, felt at y {FeltY:0.000}, " +
            $"walls {WallThickness:0.000} thick, lost past {LostRadius:0.000} or below {LostBelowY:0.000}";
    }
}

using Godot;

namespace Game.Tray
{
    // tray size in the tray's own space, derived from the scene by DiceTray; the one place that answers it
    public sealed class TrayBounds
    {
        public float HalfWidth { get; }

        public float HalfDepth { get; }

        // the felt surface: the top of the floor box, not its centre; dice rest on it
        public float FeltY { get; }

        // walls stand on the floor's outer edge, so the usable felt is this much narrower at each side
        public float WallThickness { get; }

        // past this multiple of the half-diagonal a die is gone, not just near a wall; generous, since a false escape is worse than a slow one
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

        // the felt a label may use: a name past these edges slides under the woodwork and looks like a die never marked
        public float FeltSideEdge => HalfWidth - WallThickness;

        public float FeltNearEdge => HalfDepth - WallThickness;

        // corner to centre: the furthest a die can be and still be on the felt, which is what the escape radius scales from
        public float HalfDiagonal => new Vector2(HalfWidth, HalfDepth).Length();

        public float LostRadius => HalfDiagonal * LostMargin;

        public float LostBelowY => FeltY - LostDrop;

        // true when the numbers describe a tray a die could land in; a floor smaller than its walls has no felt
        public bool IsUsable =>
            HalfWidth > WallThickness && HalfDepth > WallThickness && WallThickness >= 0f;

        // the tray dice_tray.tscn was authored with, and the fallback when the scene can't be measured; a test holds these to the scene's real values
        public static readonly TrayBounds Shipped = new(0.32f, 0.245f, 0f, 0.02f);

        // developer only, not localized, must never reach the screen
        public override string ToString() =>
            $"floor {HalfWidth * 2f:0.000} x {HalfDepth * 2f:0.000}, felt at y {FeltY:0.000}, " +
            $"walls {WallThickness:0.000} thick, lost past {LostRadius:0.000} or below {LostBelowY:0.000}";
    }
}

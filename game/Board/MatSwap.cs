using Content.Places;
using Godot;

namespace Game.Board
{
    // HOW ONE MAT COMES AWAY AND THE NEXT GOES DOWN.
    //
    // The map is a mat laid on the table, not the table's surface, so changing place is a pair of
    // hands lifting one sheet away and laying another down. Everything else on the table - the
    // tray, the sheet, the screen, the companion - stays exactly where it is, which is the whole
    // reason a place change costs a gesture rather than a scene.
    //
    // Pure, like MiniStep: it is a timeline and an offset, and the caller does the moving. That
    // buys the one property worth holding to - the table is BARE between the two mats, and the
    // new place is never laid on top of the old one.
    public sealed class MatSwap
    {
        public const float LiftSeconds = 0.34f;

        // the beat with nothing on the table. Short: a pause here reads as a load, not as a DM
        public const float BareSeconds = 0.12f;

        public const float LaySeconds = 0.38f;

        // how far off the table a mat is carried, and how far toward the DM it goes and comes from
        public const float LiftHeight = 0.05f;

        public const float AwayDepth = 0.55f;

        // the two the hands already have; a mat swap invents no gesture
        public const Gesture Lifts = Gesture.Withdraw;

        public const Gesture Lays = Gesture.Place;

        public float LiftEnds => LiftSeconds;

        public float BareEnds => LiftEnds + BareSeconds;

        public float Duration => BareEnds + LaySeconds;

        public bool IsDone(float seconds) => seconds >= Duration;

        // the moment the old mat is gone and the next one can be built. An interval test, so a
        // frame long enough to step over it still builds the new place exactly once
        public bool LaysBetween(float was, float now) => was < LiftEnds && now >= LiftEnds;

        // true while there is no mat under the pieces, which is when nothing may be stood on it
        public bool Bare(float seconds) => seconds >= LiftEnds && seconds < BareEnds;

        // where the mat being handled sits, relative to where a mat rests on the table. Before the
        // hand-off this is the old one on its way out; after it, the new one on its way in
        public Vector3 At(float seconds)
        {
            if (seconds <= 0f) return Vector3.Zero;

            if (seconds < LiftEnds) return Carried(Smooth(seconds / LiftSeconds));

            if (seconds < BareEnds) return Carried(1f);

            if (seconds >= Duration) return Vector3.Zero;

            return Carried(1f - Smooth((seconds - BareEnds) / LaySeconds));
        }

        // share of the carry spent rising. The lift LEADS the travel: a mat that goes up and
        // sideways at the same rate drags across whatever is standing on it for the first third
        public const float RiseShare = 0.25f;

        // up first, then out, and the reverse coming back - so the mat is clear of the pieces for
        // the whole of the journey and touches down only over its own square
        static Vector3 Carried(float away) =>
            new Vector3(0f,
                        LiftHeight * Smooth(Mathf.Min(away / RiseShare, 1f)),
                        -AwayDepth * away);

        static float Smooth(float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"mat swap: {LiftSeconds:0.00}s away, {BareSeconds:0.00}s of bare table, " +
            $"{LaySeconds:0.00}s down - {Duration:0.00}s in all";
    }
}

using Godot;

namespace Game.Board
{
    // one move of one piece, as a curve in time - lift, slide, hesitate, set down.
    //
    // THIS IS THE MILESTONE. B1 is the board's "control the value": a piece that teleports
    // between squares is a spreadsheet with a mesh on it, and a piece that is picked up, carried,
    // held a moment over the square and then put down is a game being played. THE_TABLE.md 3
    // asks for the hesitation by name - a brief hover before a decisive placement reads as
    // deliberation, and deliberation is what makes the move feel chosen rather than applied.
    //
    // The four phases and why each is there:
    //
    //   lift      2 mm off the felt. not a pick-up - the piece SLIDES, THE_BOARD.md B1 - but a
    //             piece dragged flat scrapes, and a piece floating flies. this is a shove
    //   slide     the travel, eased in and out, at a speed rather than a fixed duration, so a
    //             square next door is quick and the far corner takes a moment
    //   hover     the hesitation. rises, then HOLDS. the hold is the whole beat: motion that
    //             merely slows reads as damping, motion that stops reads as a decision
    //   set down  fast and accelerating. the click lands at the end of it
    //
    // Pure, and Godot-free in the sense game.tests means - Vector3 is a managed struct, so the
    // choreography can be held to its promises without an engine, exactly as TrayBounds and
    // NudgeThenRethrow are. Mini owns the clock and the mesh; this owns the shape of the move
    // and nothing else. "It never ends up between cells" is B1's verify line, and the reason it
    // can be trusted is that At(Duration) returns the destination itself rather than the last
    // step of an integration.
    public sealed class MiniStep
    {
        // ---- the choreography, in seconds and metres ----

        public const float LiftSeconds = 0.08f;

        public const float LiftHeight = 0.002f;

        // metres per second across the felt. a hand moving a piece, not a piece being flicked
        public const float SlideSpeed = 0.5f;

        // even next door takes this long - below it the move is a jump with a curve on it
        public const float MinSlideSeconds = 0.18f;

        // and corner to corner never takes longer than this, however big the board gets
        public const float MaxSlideSeconds = 0.90f;

        public const float HoverSeconds = 0.16f;

        public const float HoverHeight = 0.012f;

        // of the hover, the share spent rising. the rest is the hold, and the hold is the point
        public const float HoverRiseShare = 0.35f;

        // short and accelerating - a placement, not a landing
        public const float SetDownSeconds = 0.07f;

        // ---- one move ----

        public Vector3 From { get; }

        public Vector3 To { get; }

        public float SlideSeconds { get; }

        public MiniStep(Vector3 from, Vector3 to)
        {
            From = from;
            To = to;

            // measured on the felt, not through the lift - the height is choreography and must
            // not make a move take longer
            float across = new Vector2(to.X - from.X, to.Z - from.Z).Length();

            SlideSeconds = Mathf.Clamp(across / SlideSpeed, MinSlideSeconds, MaxSlideSeconds);
        }

        public float LiftEnds => LiftSeconds;

        public float SlideEnds => LiftEnds + SlideSeconds;

        // the moment it commits, and the moment the hand lets go
        public float HoverEnds => SlideEnds + HoverSeconds;

        public float Duration => HoverEnds + SetDownSeconds;

        public bool IsDone(float seconds) => seconds >= Duration;

        // the click, and exactly once: the frame that crossed the end of the set-down. asked as
        // an interval rather than "are we there yet" because a frame can step clean over the
        // moment, and a piece that lands silently every twentieth move is worse than one that
        // never makes a sound - the second gets fixed
        public bool SetsDownBetween(float was, float now) => was < Duration && now >= Duration;

        // where the piece is, in the board's own space
        //
        // total, not incremental. the ends are returned exactly rather than arrived at, which is
        // what stops a piece drifting off its square over a long session
        public Vector3 At(float seconds)
        {
            if (seconds <= 0f) return From;

            if (seconds >= Duration) return To;

            if (seconds < LiftEnds)
                return Raised(From, LiftHeight * Smooth(seconds / LiftSeconds));

            if (seconds < SlideEnds)
            {
                float through = Smooth((seconds - LiftEnds) / SlideSeconds);

                return Raised(From.Lerp(To, through), LiftHeight);
            }

            if (seconds < HoverEnds)
            {
                float through = (seconds - SlideEnds) / HoverSeconds;

                // rises, then holds. Min is the hold
                float rise = Smooth(Mathf.Min(through / HoverRiseShare, 1f));

                return Raised(To, Mathf.Lerp(LiftHeight, HoverHeight, rise));
            }

            // squared, so it leaves the hover gently and arrives fast - a piece being put down
            // rather than lowered
            float falling = (seconds - HoverEnds) / SetDownSeconds;

            return Raised(To, HoverHeight * (1f - falling * falling));
        }

        static Vector3 Raised(Vector3 point, float by) => new Vector3(point.X, point.Y + by, point.Z);

        // smoothstep: starts and ends at rest, which is what a hand does
        static float Smooth(float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            $"{From} -> {To}, {Duration:0.00}s ({SlideSeconds:0.00}s of it sliding)";
    }
}

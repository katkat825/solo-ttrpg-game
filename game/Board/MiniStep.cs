using System.Collections.Generic;
using Godot;

namespace Game.Board
{
    public sealed class MiniStep
    {

        public const float LiftSeconds = 0.08f;

        public const float LiftHeight = 0.002f;

        public const float SlideSpeed = 0.5f;

        public const float MinSlideSeconds = 0.18f;

        public const float MaxSlideSeconds = 0.90f;

        public const float HoverSeconds = 0.16f;

        public const float HoverHeight = 0.012f;

        // share of the hover spent rising; the rest is the hold, and the hold is the point
        public const float HoverRiseShare = 0.35f;

        public const float SetDownSeconds = 0.07f;

        // how far a refused move leans: a bit over a third of a square
        public const float RefusalLean = 0.022f;

        public Vector3 From => _through[0];

        public Vector3 To => _through[_through.Count - 1];

        public float SlideSeconds { get; }

        // one step for a whole route, not one per square; a hand lifts once and traces round
        readonly IReadOnlyList<Vector3> _through;

        // cumulative flat distance per waypoint, built once so At() is a lookup
        readonly float[] _reached;

        readonly float _length;

        public MiniStep(Vector3 from, Vector3 to) : this(new[] { from, to }) { }

        public MiniStep(IReadOnlyList<Vector3> through)
        {
            // a step to nowhere still holds, so an empty path never crashes _Process
            _through = through != null && through.Count > 0 ? through : new[] { Vector3.Zero };

            _reached = new float[_through.Count];

            for (int i = 1; i < _through.Count; i++)
                _reached[i] = _reached[i - 1] + Flat(_through[i] - _through[i - 1]);

            _length = _reached[_reached.Length - 1];

            // measured flat, not through the lift, so height never lengthens a move
            SlideSeconds = Mathf.Clamp(_length / SlideSpeed, MinSlideSeconds, MaxSlideSeconds);
        }

        // a refusal reuses the same phases and ends where it started, so the piece never leaves its square
        public static MiniStep Refusing(Vector3 from, Vector3 toward)
        {
            var away = new Vector3(toward.X - from.X, 0f, toward.Z - from.Z);

            Vector3 lean = away.LengthSquared() > 0f ? away.Normalized() * RefusalLean : Vector3.Zero;

            return new MiniStep(new[] { from, from + lean, from });
        }

        public int Waypoints => _through.Count;

        static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();

        public float LiftEnds => LiftSeconds;

        public float SlideEnds => LiftEnds + SlideSeconds;

        public float HoverEnds => SlideEnds + HoverSeconds;

        public float Duration => HoverEnds + SetDownSeconds;

        public bool IsDone(float seconds) => seconds >= Duration;

        // an interval test, so a frame stepping over the moment still fires the click once
        public bool SetsDownBetween(float was, float now) => was < Duration && now >= Duration;

        // total, not incremental: the ends are returned exactly, so a piece never drifts off its square
        public Vector3 At(float seconds)
        {
            if (seconds <= 0f) return From;

            if (seconds >= Duration) return To;

            if (seconds < LiftEnds)
                return Raised(From, LiftHeight * Smooth(seconds / LiftSeconds));

            if (seconds < SlideEnds)
            {
                float through = Smooth((seconds - LiftEnds) / SlideSeconds);

                // eased over the whole route, not per leg, or the piece stops dead at every waypoint
                return Raised(Along(through * _length), LiftHeight);
            }

            if (seconds < HoverEnds)
            {
                float through = (seconds - SlideEnds) / HoverSeconds;

                // rises, then holds; the Min is the hold
                float rise = Smooth(Mathf.Min(through / HoverRiseShare, 1f));

                return Raised(To, Mathf.Lerp(LiftHeight, HoverHeight, rise));
            }

            float falling = (seconds - HoverEnds) / SetDownSeconds;

            return Raised(To, HoverHeight * (1f - falling * falling));
        }

        Vector3 Along(float distance)
        {
            if (_through.Count == 1 || _length <= 0f) return From;

            for (int i = 1; i < _through.Count; i++)
            {
                if (distance > _reached[i] && i < _through.Count - 1) continue;

                float leg = _reached[i] - _reached[i - 1];
                float into = leg <= 0f ? 1f : Mathf.Clamp((distance - _reached[i - 1]) / leg, 0f, 1f);

                return _through[i - 1].Lerp(_through[i], into);
            }

            return To;
        }

        static Vector3 Raised(Vector3 point, float by) => new Vector3(point.X, point.Y + by, point.Z);

        static float Smooth(float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // developer only, not localized, must never reach the screen
        public override string ToString() =>
            $"{From} -> {To} through {Waypoints} points, {_length:0.000}m, " +
            $"{Duration:0.00}s ({SlideSeconds:0.00}s of it sliding)";
    }
}

using Godot;

namespace Game.Room
{
    // LOOKING CLOSELY AT SOMETHING IS A THING YOU DO AS A PERSON AT A TABLE.
    //
    // Not a zoom slider and not a magnifier: you lean in over the thing, or you pick it up. Which
    // of the two depends on the thing - a card, a note and the character sheet come up in your
    // hands, and the mat, the screen and the corkboard are leaned toward, because you do not pick
    // up a table.
    //
    // THE ANGLE NEVER CHANGES. Keeping the camera's own basis and sliding the eye along the way it
    // is already looking is what makes this read as leaning rather than as orbiting - and it lands
    // the thing dead centre for free, since a point exactly ahead of the eye is a point in the
    // middle of the picture.
    //
    // Pure geometry and a timeline, like MiniStep: the caller moves the camera, having been told.
    public sealed class Lean
    {
        public const float InSeconds = 0.40f;

        // looking away is quicker than looking closer. You lean in deliberately and sit back
        // without thinking about it
        public const float OutSeconds = 0.26f;

        // how close the eye gets to a fixed thing. It stops short: a camera that arrives AT the
        // corkboard is inside the wall, not reading the note on it
        public const float Nearest = 0.26f;

        // where a thing you picked up is held - ahead of you and a little below the eye line,
        // which is where a hand holds a piece of paper somebody is reading
        public const float Held = 0.34f;

        public const float HeldBelow = 0.09f;

        Lean(Transform3D from, Transform3D to, float seconds)
        {
            From = from;
            To = to;
            Seconds = seconds <= 0f ? 0.0001f : seconds;
        }

        public Transform3D From { get; }

        public Transform3D To { get; }

        public float Seconds { get; }

        public bool IsDone(float seconds) => seconds >= Seconds;

        public Transform3D At(float seconds)
        {
            if (seconds <= 0f) return From;

            if (seconds >= Seconds) return To;

            return From.InterpolateWith(To, Smooth(seconds / Seconds));
        }

        // leaning in on something that stays where it is
        public static Lean Toward(Transform3D eye, Vector3 thing, float near = Nearest) =>
            new Lean(eye, new Transform3D(eye.Basis, Over(eye, thing, near)), InSeconds);

        // and sitting back again
        public static Lean Away(Transform3D eye, Transform3D overview) =>
            new Lean(eye, overview, OutSeconds);

        // the eye, slid along the way it is already looking until the thing is `near` ahead of it
        public static Vector3 Over(Transform3D eye, Vector3 thing, float near = Nearest)
        {
            Vector3 looking = -eye.Basis.Z;

            if (looking.LengthSquared() < 0.0000001f) return eye.Origin;

            return thing - looking.Normalized() * near;
        }

        // WHERE A THING YOU PICKED UP IS, which is in front of your face and not where it was
        // lying. The camera does not move for this one - the object does, which is the whole
        // difference between picking something up and leaning over it.
        public static Vector3 InHand(Transform3D eye, float held = Held)
        {
            Vector3 looking = -eye.Basis.Z;

            if (looking.LengthSquared() < 0.0000001f) return eye.Origin;

            return eye.Origin + looking.Normalized() * held + Vector3.Down * HeldBelow;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"lean: {From.Origin} -> {To.Origin} over {Seconds:0.00}s";
    }
}

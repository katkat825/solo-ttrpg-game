using Godot;

namespace Game.Room
{
    // WHAT THE FIXED CAMERA CAN ACTUALLY SEE, AS A NUMBER.
    //
    // The camera does not move (table.tscn says so at the line), so whether a thing on this table
    // is in the picture is arithmetic - and it was being done by hand, once per eye check, and
    // forgotten in between. The character sheet was A3 and hung off the right of the frame; the
    // hint cord hung off the left; a speech card grew from its middle until its corner left the
    // side; the whole stack of cards you are carrying sat 12 cm below the bottom edge. Four
    // findings, one class, and the only thing they had in common was that nobody could ask the
    // question without opening the editor.
    //
    // So the question is asked here instead. TableView owns which way the player is LOOKING; this
    // owns what they can SEE, and the two are the whole of what the fixed camera knows.
    //
    // It is deliberately pure - Godot's vector structs and nothing that needs an engine behind it -
    // so game.tests can hold the arithmetic and a headless check can hold the shipped table.
    public readonly struct Framing
    {
        // what the game is composed for. A wider monitor sees more, never less: Godot keeps the
        // vertical angle and widens, which is why the sheet and the cord went off the SIDES first
        public const float Widescreen = 16f / 9f;

        readonly Transform3D _eye;

        readonly float _halfWide;

        readonly float _halfTall;

        // fov is the VERTICAL angle, which is what Godot means by it unless a camera has been told
        // to keep its width instead - see Of below, which is the only place that distinction lives
        public Framing(Transform3D eye, float verticalFovDegrees, float aspect = Widescreen)
        {
            _eye = eye;
            _halfTall = Mathf.Tan(Mathf.DegToRad(verticalFovDegrees) * 0.5f);
            _halfWide = _halfTall * aspect;
        }

        public static Framing Of(Camera3D camera, float aspect = Widescreen)
        {
            if (camera == null) return default;

            // KEEP_WIDTH means the number on the camera is the horizontal angle, so the vertical
            // one this is built from has to be worked back out of it
            float vertical = camera.KeepAspect == Camera3D.KeepAspectEnum.Width
                ? Mathf.RadToDeg(2f * Mathf.Atan(Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f) / aspect))
                : camera.Fov;

            return new Framing(camera.GlobalTransform, vertical, aspect);
        }

        public bool Exists => _halfTall > 0f;

        // how far in front of the eye a point is, along the way it is looking. Behind it is negative
        public float Depth(Vector3 at) => -(_eye.AffineInverse() * at).Z;

        public float HalfWidth(float depth) => depth * _halfWide;

        public float HalfHeight(float depth) => depth * _halfTall;

        // HOW MANY METRES INSIDE THE NEAREST EDGE OF THE PICTURE A POINT IS, and negative is the
        // number that matters: it is how far off the side it has fallen, which is what a failure
        // wants to say. Measured at the point's own depth, because the frame narrows toward the
        // player and a thing near the front has much less room than the same thing at the back -
        // that is exactly why a long speech card failed where a short one passed.
        public float Margin(Vector3 at)
        {
            if (!Exists) return 0f;

            Vector3 seen = _eye.AffineInverse() * at;

            float depth = -seen.Z;

            if (depth <= 0f) return depth - 0.0001f;

            return Mathf.Min(HalfWidth(depth) - Mathf.Abs(seen.X),
                             HalfHeight(depth) - Mathf.Abs(seen.Y));
        }

        public bool Holds(Vector3 at) => Exists && Margin(at) >= 0f;

        // the meanest corner of a box, so a thing with a size is judged by its worst edge rather
        // than by the middle of it - the sheet's centre was always in frame and its right edge was not
        public float Margin(Vector3 at, Vector3 span)
        {
            if (!Exists) return 0f;

            var half = new Vector3(Mathf.Abs(span.X) * 0.5f,
                                   Mathf.Abs(span.Y) * 0.5f,
                                   Mathf.Abs(span.Z) * 0.5f);

            float worst = float.MaxValue;

            for (int corner = 0; corner < 8; corner++)
            {
                var offset = new Vector3((corner & 1) == 0 ? -half.X : half.X,
                                         (corner & 2) == 0 ? -half.Y : half.Y,
                                         (corner & 4) == 0 ? -half.Z : half.Z);

                worst = Mathf.Min(worst, Margin(at + offset));
            }

            return worst;
        }

        public bool Holds(Vector3 at, Vector3 span) => Exists && Margin(at, span) >= 0f;

        // WHAT A METRE IS WORTH IN PIXELS at a given depth, which is the only honest way to ask
        // whether a word on this table can be read. A Label3D's height in the world means nothing
        // on its own - 16 mm of type is enormous on a card held in your hand and nine pixels tall
        // seen from across a room, and this camera is a metre and a half back from a table.
        public float PixelsPerMetre(float depth, float screenHeight = Screen)
        {
            float half = HalfHeight(depth);

            return half <= 0f ? 0f : screenHeight / (2f * half);
        }

        // what the game is composed for, beside Widescreen. A player on a bigger screen reads more
        // easily, never less, so the smallest supported height is the one worth measuring against
        public const float Screen = 1080f;

        // WHERE THE PICTURE MEETS A FLAT SURFACE, which is the one thing a person laying objects
        // out on a table actually wants to know: everything nearer than this is below the bottom
        // edge. Returns the z of the near edge along the camera's own line, and NaN for a surface
        // the bottom edge never reaches.
        public float NearEdgeOn(float height)
        {
            if (!Exists) return float.NaN;

            Vector3 down = (_eye.Basis * new Vector3(0f, -_halfTall, -1f)).Normalized();

            if (Mathf.Abs(down.Y) < 0.000001f) return float.NaN;

            float along = (height - _eye.Origin.Y) / down.Y;

            return along < 0f ? float.NaN : _eye.Origin.Z + along * down.Z;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            Exists
                ? $"{Mathf.RadToDeg(2f * Mathf.Atan(_halfTall)):0.0} deg tall, " +
                  $"{Mathf.RadToDeg(2f * Mathf.Atan(_halfWide)):0.0} deg wide, from {_eye.Origin}"
                : "no camera";
    }
}

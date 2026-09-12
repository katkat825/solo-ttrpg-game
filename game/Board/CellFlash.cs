using Godot;

namespace Game.Board
{
    // what a refused move looks like: the square you asked for lights up, once, and goes out.
    //
    // THE_BOARD.md's architecture note is the whole brief - "a refused move is shown on the board,
    // not in a dialog" - and THE_TABLE.md 6 is why: the room is the entire UI. There is no message
    // box on a table. What there is, when you point at a square you cannot get to, is somebody
    // tapping it and shaking their head, and this is the half of that the board can say. The other
    // half is the piece itself leaning toward the square and settling back (MiniStep.Refusing).
    //
    // A SQUARE, NOT A RING. The tray answers in rings because a die is round and lands anywhere on
    // the felt; the board is made of squares and a refusal is about one of them. Borrowing
    // Tray/FeltRing would have coupled the board to the tray to say something in the wrong shape.
    //
    // Told apart from everything else on the board by BEING BRIEF, the lesson SnagFlash wrote down:
    // nothing else here appears and vanishes, so half a second of light is unmistakable and needs
    // no colour anyone has to learn.
    public partial class CellFlash : Node3D
    {
        // long enough to see, short enough that a second click is never waiting on it. it outlasts
        // the piece's own lean by a hair, so the two read as one gesture
        [Export] public float Seconds { get; set; } = 0.5f;

        // of that, the share held at full strength before it starts to fade. a flash that begins
        // fading immediately reads as a fault in the rendering rather than as an answer
        [Export] public float HoldShare { get; set; } = 0.25f;

        // a hair above the painted grid lines, so a refused square lights the square and not a
        // stripe through it
        public const float Lift = 0.0016f;

        MeshInstance3D _lit;

        StandardMaterial3D _ink;

        float _left;

        // the flat square that gets lit, cell-sized - handed over rather than worked out, because
        // the board is the one place that knows how big a square is
        public Mesh Square { get; set; }

        public Color Tint { get; set; } = new Color(0.62f, 0.18f, 0.14f, 0.5f);

        public override void _Ready()
        {
            // unshaded on purpose: this is a mark on the map, not an object lying on it, so it must
            // not take the room's light and must not cast anything
            _ink = new StandardMaterial3D
            {
                AlbedoColor = Tint,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };

            AddChild(_lit = new MeshInstance3D
            {
                Name = "Lit",
                Mesh = Square,
                MaterialOverride = _ink,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });

            Visible = false;
        }

        // over that square, now. board-local, like everything else on this node
        public void Show(Vector3 at)
        {
            if (_lit == null) return;

            Position = at + new Vector3(0f, Lift, 0f);
            _left = Seconds;
            Visible = true;

            Fade(1f);
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;

            _left -= (float)delta;

            if (_left <= 0f)
            {
                Visible = false;
                return;
            }

            float through = 1f - _left / Mathf.Max(Seconds, 0.001f);
            float hold = Mathf.Clamp(HoldShare, 0f, 0.9f);

            Fade(through <= hold ? 1f : 1f - (through - hold) / (1f - hold));
        }

        void Fade(float strength)
        {
            if (_ink == null) return;

            Color ink = Tint;
            ink.A = Tint.A * Mathf.Clamp(strength, 0f, 1f);
            _ink.AlbedoColor = ink;
        }
    }
}

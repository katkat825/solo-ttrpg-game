using Godot;

namespace Game.Board
{
    public partial class CellFlash : Node3D
    {
        // outlasts the piece's own lean by a hair, so the two read as one gesture
        [Export] public float Seconds { get; set; } = 0.5f;

        // share held at full strength before it fades; fading from the start looks like a rendering fault
        [Export] public float HoldShare { get; set; } = 0.25f;

        // a hair above the grid lines, so it lights the square not a stripe
        public const float Lift = 0.0016f;

        MeshInstance3D _lit;

        StandardMaterial3D _ink;

        float _left;

        // handed over, cell-sized: the board is the one place that knows a square's size
        public Mesh Square { get; set; }

        public Color Tint { get; set; } = new Color(0.62f, 0.18f, 0.14f, 0.5f);

        public override void _Ready()
        {
            // unshaded: a mark on the map, not an object, so it takes no light and casts nothing
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

using Godot;
using Game.Dice;

namespace Game.Tray
{
    public partial class SnagFlash : Node3D
    {
        // followed every frame, like DieMark - a bumped die takes its flash with it
        public DieBody Die { get; set; }

        // the die's own mark's outer edge (DieMark.OuterRadiusFor); this sits outside it so the two never overlap
        public float InnerRadius { get; set; }

        // felt position and the frame it's measured in, the same pair DieMark takes
        public TrayBounds Bounds { get; set; } = TrayBounds.Shipped;

        public Node3D TraySpace { get; set; }

        const float FeltLift = 0.0014f;  // a hair above DieMark's rings, so the two never z-fight

        const float Gap = 0.0016f;

        // thinner than the counted ring, which is 0.0028 - this says "and also", not "instead"
        const float Thickness = 0.0018f;

        const int Pings = 2;

        const float PingSeconds = 0.28f;

        const float PingReach = 1.28f;

        // what it settles to and holds: visible, but never bright enough to be mistaken for the two rings that mean something
        const float RestAlpha = 0.20f;

        // pale and cool, deliberately not red: a Snag has no effect and must never read as a failed roll
        static readonly Color Ink = new(0.84f, 0.88f, 0.95f);

        MeshInstance3D _ring;
        StandardMaterial3D _ringInk;

        double _age;

        public override void _Ready()
        {
            float from = InnerRadius + Gap;

            _ringInk = FeltRing.Ink(Ink with { A = RestAlpha });

            AddChild(_ring = new MeshInstance3D
            {
                Name = "Ring",
                Mesh = FeltRing.Build(from, from + Thickness),
                MaterialOverride = _ringInk,

                // drawn as light, like every other mark - no shadow of its own
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });

            // placed before it is ever drawn, for the reason DieMark says at the same line
            Follow();
        }

        // follow the die, not a snapshot, or a ring is left around empty felt; in tray space, as DieMark does
        void Follow()
        {
            Vector3 p = TraySpace?.ToLocal(Die.GlobalPosition) ?? Die.GlobalPosition;
            Position = new Vector3(p.X, Bounds.FeltY + FeltLift, p.Z);
        }

        public override void _Process(double delta)
        {
            Follow();

            if (_age >= Pings * PingSeconds) return;

            _age += delta;

            // out and back within each ping (a sine), so it stays continuous with the resting size instead of snapping back
            float phase = (float)(_age / PingSeconds) % 1f;
            float ping = Mathf.Sin(Mathf.Pi * phase);

            _ring.Scale = Vector3.One * Mathf.Lerp(1f, PingReach, ping);
            _ringInk.AlbedoColor = Ink with { A = Mathf.Lerp(RestAlpha, 1f, ping) };

            if (_age < Pings * PingSeconds) return;

            // snap exactly to the resting state at the end, so a dropped frame can't leave the ring stuck bright or large
            _ring.Scale = Vector3.One;
            _ringInk.AlbedoColor = Ink with { A = RestAlpha };
        }
    }
}

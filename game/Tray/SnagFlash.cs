using Godot;
using Game.Dice;

namespace Game.Tray
{
    // what a Snag looks like on the felt: a ring outside the die's own mark that pings twice
    // and then goes quiet, staying until the next throw
    //
    // the die that snagged is ALSO either a counted die or the Impact die, so this cannot be
    // another colour of DieMark - it has to layer over one. it stays on the felt because that is
    // where this tray writes; a die that lit itself up would be the one object on the table
    // pretending to be a screen, and M6 spent a whole milestone deciding otherwise
    //
    // told apart by MOVEMENT rather than by colour, which is the part that matters. counted gold
    // and Impact ember already own the warm end, and both are steady - the Impact die breathes
    // once every 2.4 seconds, which is slow enough to read as alive. this pings twice in half a
    // second and stops. nothing else on the felt is quick
    //
    // it moves a little and not a lot, on purpose. a Snag is the smallest event in the game -
    // no mechanical effect at all - and a ring thrown wide would oversell it, as well as running
    // out under the woodwork from a die that settled near a wall
    //
    // what it leaves behind is what makes M9 verifiable by looking: the ping is over in half a
    // second, and "throw fifty times and count the flashes" fails if a blink costs you one
    public partial class SnagFlash : Node3D
    {
        // followed every frame, like DieMark - a bumped die takes its flash with it
        public DieBody Die { get; set; }

        // where the die's own mark stops, from DieMark.OuterRadiusFor
        // this sits outside it, so the two never overlap however the die was marked
        public float InnerRadius { get; set; }

        // where the felt is, and the frame that is measured in - the same pair DieMark takes,
        // and for the same reason: this ring was pinned to a world Y until F2
        public TrayBounds Bounds { get; set; } = TrayBounds.Shipped;

        public Node3D TraySpace { get; set; }

        const float FeltLift = 0.0014f;  // a hair above DieMark's rings, so the two never z-fight

        const float Gap = 0.0016f;       // the die's mark to this one

        // thinner than the counted ring, which is 0.0028 - this says "and also", not "instead"
        const float Thickness = 0.0018f;

        const int Pings = 2;

        const float PingSeconds = 0.28f;

        const float PingReach = 1.28f;   // multiples of the resting radius

        // what it settles to and holds - visible, and never bright enough to be mistaken for one
        // of the two rings that mean something
        const float RestAlpha = 0.20f;

        // pale and cool against counted gold and Impact ember, and deliberately not red:
        // a Snag has no mechanical effect and must never read as a roll that failed
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

        // follow the die rather than snapshotting where it was, for the same reason DieMark
        // does - a ring around empty felt lies. in the tray's space, also for DieMark's reason
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

            // out and back within each ping, so the ring is continuous with where it rests -
            // a sawtooth would snap back to the resting size in one frame, twice
            float phase = (float)(_age / PingSeconds) % 1f;
            float ping = Mathf.Sin(Mathf.Pi * phase);

            _ring.Scale = Vector3.One * Mathf.Lerp(1f, PingReach, ping);
            _ringInk.AlbedoColor = Ink with { A = Mathf.Lerp(RestAlpha, 1f, ping) };

            if (_age < Pings * PingSeconds) return;

            // landed exactly on the resting state rather than wherever the last frame fell,
            // so a dropped frame cannot leave the ring a little bright or a little large forever
            _ring.Scale = Vector3.One;
            _ringInk.AlbedoColor = Ink with { A = RestAlpha };
        }
    }
}

using Godot;
using Core.Localization;
using Game.Dice;

namespace Game.Tray
{
    // the Impact die didn't count but is the one thrown again for damage, so it's marked as the live die, not the spare
    public partial class DieMark : Node3D
    {
        // followed every frame, so a bumped die takes its ring with it
        public DieBody Die { get; set; }

        // a key - attr.might.name, never "Might"
        public string LabelKey { get; set; }

        public DieRole Role { get; set; }

        // injected, so this file cannot reach for TranslationServer
        public ILocalizer Text { get; set; }

        // tray size and felt position, measured off the scene by DiceTray
        public TrayBounds Bounds { get; set; } = TrayBounds.Shipped;

        // the frame Bounds is expressed in; null means the tray stands at the world origin
        public Node3D TraySpace { get; set; }

        const float FeltLift = 0.0012f;  // clear of the felt, so the ring never fights the floor for the pixel

        const float RingGap = 0.003f;    // die's furthest corner to the inside of its ring

        const float CountedThickness = 0.0028f;
        const float ImpactThickness = 0.0060f;

        const float HaloGap = 0.0045f;
        const float HaloThickness = 0.0014f;

        const float LabelHeight = 0.0125f;

        const float LabelGap = 0.008f;

        const int GlyphResolution = 64;

        const float BreathSeconds = 2.4f;   // slow enough to read as alive, not as a blink

        static readonly Color CountedInk = new(0.92f, 0.84f, 0.55f);
        static readonly Color ImpactInk = new(1.00f, 0.47f, 0.13f);

        static readonly Color SpareInk = new(0.42f, 0.46f, 0.44f);

        Label3D _label;
        MeshInstance3D _ring;
        MeshInstance3D _halo;
        StandardMaterial3D _ringInk;
        StandardMaterial3D _haloInk;

        float _labelDrop;

        double _breath;

        public override void _Ready()
        {
            Build();
            Retranslate();

            // place it before it's ever drawn: a mark built this frame gets no _Process until next, and a ring at the tray centre reads as a fourth die
            Follow();
        }

        // switching language, or turning the pseudolocale on, rewrites the felt without re-throwing
        public override void _Notification(int what)
        {
            if (what == NotificationTranslationChanged) Retranslate();
        }

        public void Retranslate()
        {
            if (_label != null && Text != null) _label.Text = Text.Get(LabelKey);
        }

        // where the mark's outer edge lands, so a name or snag flash places against one formula, not a copy of the constants; a role of None still reserves the ring's space
        public static float OuterRadiusFor(DieSolid solid, DieRole role)
        {
            float outer = solid.Circumradius + RingGap
                        + (role == DieRole.Impact ? ImpactThickness : CountedThickness);

            return role == DieRole.Impact ? outer + HaloGap + HaloThickness : outer;
        }

        void Build()
        {
            Color ink = Role switch
            {
                DieRole.Counted => CountedInk,
                DieRole.Impact => ImpactInk,
                _ => SpareInk,
            };

            float inner = Die.Solid.Circumradius + RingGap;
            float outer = inner + (Role == DieRole.Impact ? ImpactThickness : CountedThickness);

            if (Role != DieRole.None)
            {
                _ringInk = FeltRing.Ink(ink);
                _ring = Ring(FeltRing.Build(inner, outer), _ringInk);
            }

            if (Role == DieRole.Impact)
            {
                // the second ring turns the ember colour from "a different one" into "still going"
                _haloInk = FeltRing.Ink(ink);
                _halo = Ring(FeltRing.Build(outer + HaloGap, outer + HaloGap + HaloThickness), _haloInk);
            }

            _labelDrop = OuterRadiusFor(Die.Solid, Role) + LabelGap + LabelHeight * 0.5f;

            AddChild(_label = new Label3D
            {
                Name = "Name",

                // deliberately the raw key: if the lookup were ever skipped, the felt reads attr.might.name, impossible to mistake for a translation
                Text = LabelKey,

                FontSize = GlyphResolution,
                PixelSize = LabelHeight / GlyphResolution,
                Modulate = ink,
                OutlineSize = 0,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                DoubleSided = false,
                AlphaCut = Label3D.AlphaCutMode.Discard,

                // disabled: Godot would translate the Label3D itself, a second lookup that double-brackets under the pseudolocale
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,

                // lying on the felt, glyph tops away from the camera, so it reads upright from the near side
                Transform = new Transform3D(
                    new Basis(Vector3.Right, Vector3.Forward, Vector3.Up),
                    new Vector3(0f, 0f, _labelDrop)),
            });
        }

        // below the die, above it when below would hit the near wall, slid along when the word is long
        // dieAt and the edges are both in tray space, so the whole comparison stays in one frame and the tray can stand anywhere
        void PlaceLabel(Vector3 dieAt)
        {
            float z = dieAt.Z + _labelDrop > Bounds.FeltNearEdge ? -_labelDrop : _labelDrop;

            // measured every frame, not cached: a Label3D builds its mesh after the fact, so asking as the text changes answers about the last one
            float half = _label.GetAabb().Size.X * 0.5f;

            float side = Bounds.FeltSideEdge;
            float onFelt = Mathf.Clamp(dieAt.X, -side + half, side - half);

            // an offset within the mark, not a position on the felt, so it needs no conversion
            _label.Position = new Vector3(onFelt - dieAt.X, 0f, z);
        }

        MeshInstance3D Ring(Mesh mesh, Material ink)
        {
            var instance = new MeshInstance3D
            {
                Name = "Ring",
                Mesh = mesh,
                MaterialOverride = ink,

                // marks are drawn light, not lit - no shadow of their own, and none off the die inside them
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            AddChild(instance);
            return instance;
        }

        // follow the die rather than snapshot: a die knocked loose would leave its ring behind on empty felt
        // answered in tray space (Position, not GlobalPosition), because TrayMarks holds itself on the tray's origin
        void Follow()
        {
            Vector3 p = TraySpace?.ToLocal(Die.GlobalPosition) ?? Die.GlobalPosition;
            Position = new Vector3(p.X, Bounds.FeltY + FeltLift, p.Z);

            PlaceLabel(p);
        }

        public override void _Process(double delta)
        {
            Follow();

            if (Role != DieRole.Impact) return;

            _breath += delta;

            // never all the way down - a mark that fades out entirely reads as one being dismissed
            float pulse = 0.5f + 0.5f * Mathf.Cos(Mathf.Tau * (float)(_breath / BreathSeconds));

            _ringInk.AlbedoColor = ImpactInk with { A = Mathf.Lerp(0.62f, 1.00f, pulse) };
            _haloInk.AlbedoColor = ImpactInk with { A = Mathf.Lerp(0.10f, 0.45f, pulse) };
            _halo.Scale = Vector3.One * Mathf.Lerp(1.00f, 1.05f, pulse);
        }
    }
}

using Godot;
using Core.Localization;

// one die's mark on the felt - a ring around it and its name written beside it
// no panel and no overlay: the marks lie on the table with the dice and read from the same angle
// the Impact die is the one that did NOT count, but it is the one picked up and thrown for damage
// so it is marked as the live die rather than the spare - heavier ring, ember colour, halo, slow breath
// draws what Mark says and decides nothing itself

public partial class DieMark : Node3D
{
    // followed every frame, so a bumped die takes its ring with it
    public DieBody Die { get; set; }

    // a key - attr.might.name, never "Might"
    public string LabelKey { get; set; }

    public TrayMark Mark { get; set; }

    // injected, so this file cannot reach for TranslationServer
    public ILocalizer Text { get; set; }

    // the felt as the label may use it - a name past these edges vanishes into the woodwork
    // assumes the tray is centred on the world origin, as DieBody.LostRadius does
    public float FeltNearEdge { get; set; } = 0.225f;

    public float FeltSideEdge { get; set; } = 0.300f;

    const float FeltLift = 0.0012f;  // clear of the felt, so the ring never fights the floor for the pixel

    const float RingGap = 0.003f;    // die's furthest corner to the inside of its ring

    const float CountedThickness = 0.0028f;
    const float ImpactThickness = 0.0060f;

    const float HaloGap = 0.0045f;   // Impact ring to the second ring outside it
    const float HaloThickness = 0.0014f;

    const float LabelHeight = 0.0125f;  // cap height on the felt, in metres - the tray is 0.64 m across

    const float LabelGap = 0.008f;

    const int GlyphResolution = 64;

    const float BreathSeconds = 2.4f;   // slow enough to read as alive, not as a blink

    // far enough apart to tell at a glance on dark green felt, and neither is a "wrong answer" red
    static readonly Color CountedInk = new(0.92f, 0.84f, 0.55f);
    static readonly Color ImpactInk = new(1.00f, 0.47f, 0.13f);

    // an unmarked die still says what it is, quietly - never happens in a three-die pool
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

    // where this die's mark stops, so anything drawn outside it - the name, a snag flash -
    // is placed against one formula instead of against a second copy of the constants above
    // static and pure, so a caller can ask before the mark is built and never has to guess
    // at ordering. an unmarked die draws no ring but still reserves the space one would take
    public static float OuterRadiusFor(DieSolid solid, TrayMark mark)
    {
        float outer = solid.Circumradius + RingGap
                    + (mark == TrayMark.Impact ? ImpactThickness : CountedThickness);

        return mark == TrayMark.Impact ? outer + HaloGap + HaloThickness : outer;
    }

    void Build()
    {
        Color ink = Mark switch
        {
            TrayMark.Counted => CountedInk,
            TrayMark.Impact => ImpactInk,
            _ => SpareInk,
        };

        float inner = Die.Solid.Circumradius + RingGap;
        float outer = inner + (Mark == TrayMark.Impact ? ImpactThickness : CountedThickness);

        if (Mark != TrayMark.None)
        {
            _ringInk = FeltRing.Ink(ink);
            _ring = Ring(FeltRing.Build(inner, outer), _ringInk);
        }

        if (Mark == TrayMark.Impact)
        {
            // the second ring turns the ember colour from "a different one" into "still going"
            _haloInk = FeltRing.Ink(ink);
            _halo = Ring(FeltRing.Build(outer + HaloGap, outer + HaloGap + HaloThickness), _haloInk);
        }

        _labelDrop = OuterRadiusFor(Die.Solid, Mark) + LabelGap + LabelHeight * 0.5f;

        AddChild(_label = new Label3D
        {
            Name = "Name",

            // deliberately the key: if the locale lookup were ever skipped, the felt reads
            // attr.might.name, which is impossible to mistake for a translation
            Text = LabelKey,

            FontSize = GlyphResolution,
            PixelSize = LabelHeight / GlyphResolution,
            Modulate = ink,
            OutlineSize = 0,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            DoubleSided = false,
            AlphaCut = Label3D.AlphaCutMode.Discard,

            // Godot would translate a Label3D's text itself, a second place a key becomes words
            // with the pseudolocale on it would bracket the already bracketed string
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,

            // lying on the felt, glyph tops away from the camera, so it reads upright from the near side
            Transform = new Transform3D(
                new Basis(Vector3.Right, Vector3.Forward, Vector3.Up),
                new Vector3(0f, 0f, _labelDrop)),
        });
    }

    // below the die, above it when below would hit the near wall, slid along when the word is long
    // a name that has slid under the woodwork looks exactly like a die that was never marked
    void PlaceLabel(Vector3 dieAt)
    {
        float z = dieAt.Z + _labelDrop > FeltNearEdge ? -_labelDrop : _labelDrop;

        // measured every frame rather than cached when the text was set
        // a Label3D builds its mesh after the fact, so asking as the string changes answers about the last one
        float half = _label.GetAabb().Size.X * 0.5f;

        float onFelt = Mathf.Clamp(dieAt.X, -FeltSideEdge + half, FeltSideEdge - half);

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

    public override void _Process(double delta)
    {
        // follow the die rather than snapshotting where it was
        // a die knocked loose later would leave its ring behind, and a ring around empty felt lies
        Vector3 p = Die.GlobalPosition;
        GlobalPosition = new Vector3(p.X, FeltLift, p.Z);
        PlaceLabel(p);

        if (Mark != TrayMark.Impact) return;

        _breath += delta;

        // never all the way down - a mark that fades out entirely reads as one being dismissed
        float pulse = 0.5f + 0.5f * Mathf.Cos(Mathf.Tau * (float)(_breath / BreathSeconds));

        _ringInk.AlbedoColor = ImpactInk with { A = Mathf.Lerp(0.62f, 1.00f, pulse) };
        _haloInk.AlbedoColor = ImpactInk with { A = Mathf.Lerp(0.10f, 0.45f, pulse) };
        _halo.Scale = Vector3.One * Mathf.Lerp(1.00f, 1.05f, pulse);
    }
}

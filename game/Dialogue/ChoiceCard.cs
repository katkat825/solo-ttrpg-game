using Godot;
using Game.Companion;

namespace Game.Dialogue
{
    // One thing you could say, as a card lying on the table in front of you. Not a list box:
    // THE_TABLE.md section 6 is a hard rule, and a dialogue menu is the most tempting panel in the
    // whole game to bolt over the scene.
    //
    // A card you cannot take is shown anyway, greyed - the same way an option whose condition
    // failed is shown in a good branching game, because knowing what you cannot say is information.
    [GlobalClass]
    public partial class ChoiceCard : Node3D
    {
        [Export] public float Width { get; set; } = 0.26f;

        [Export] public float Height { get; set; } = 0.05f;

        [Export] public Color Ink { get; set; } = new Color("#221f1a");

        [Export] public Color Face { get; set; } = new Color(0.90f, 0.88f, 0.81f, 0.98f);

        [Export] public Color Lit { get; set; } = new Color(0.98f, 0.95f, 0.84f, 1f);

        // what a choice whose condition failed looks like: still there, plainly not available
        [Export] public Color Closed { get; set; } = new Color(0.62f, 0.60f, 0.57f, 0.75f);

        Label3D _text;

        MeshInstance3D _face;

        StandardMaterial3D _paper;

        StaticBody3D _body;

        // which option this is, as Yarn numbers them; -1 until it is dealt
        public int Option { get; private set; } = -1;

        public bool Offered { get; private set; }

        // developer diagnostics; the words came from the localizer and this is a copy
        public string Said { get; private set; } = "";

        public override void _Ready()
        {
            if (_face != null) return;

            _paper = new StandardMaterial3D
            {
                AlbedoColor = Face,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.9f,
            };

            _face = new MeshInstance3D
            {
                Name = "Face",
                Mesh = new QuadMesh { Size = new Vector2(Width, Height) },
                MaterialOverride = _paper,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            AddChild(_face);

            _text = new Label3D
            {
                Name = "Text",
                FontSize = 30,
                PixelSize = 0.00040f,
                Modulate = Ink,
                Width = Width / 0.00040f,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Shaded = false,
                Position = new Vector3(0f, 0f, 0.001f),
            };

            AddChild(_text);

            // a card is a thing you touch, so it has a body the way the Nerve tokens do
            _body = new StaticBody3D { Name = "Touch" };

            _body.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(Width, Height, 0.004f) },
            });

            AddChild(_body);
        }

        public void Deal(int option, string text, bool offered)
        {
            _Ready();

            Option = option;
            Offered = offered;
            Said = text ?? "";

            _text.Text = Said;
            _text.Modulate = offered ? Ink : new Color(Ink, 0.5f);
            _paper.AlbedoColor = offered ? Face : Closed;
        }

        public bool Owns(GodotObject what) => _body != null && ReferenceEquals(_body, what);

        public void Light(bool lit)
        {
            if (_paper == null || !Offered) return;

            _paper.AlbedoColor = lit ? Lit : Face;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"choice {Option}: \"{Said}\"" + (Offered ? "" : " (closed)");
    }
}

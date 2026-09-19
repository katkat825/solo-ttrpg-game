using Godot;

namespace Game.Loot
{
    // ONE THING YOU ARE CARRYING, AS A CARD.
    //
    // Face down it shows its name and nothing else, which is all a card lying in a stack needs to
    // be recognised. Turned over it shows what it does - the same card, read rather than a second
    // object appearing, because a detail panel is the thing this deletes.
    //
    // It holds TEXT, already localized, like every other piece of paper at this table.
    [GlobalClass]
    public partial class ItemCard : Node3D
    {
        [Export] public float Width { get; set; } = 0.042f;

        [Export] public float Height { get; set; } = 0.060f;

        // a turned card is read, so it is bigger than the edge it was showing in the stack
        [Export] public float ReadWidth { get; set; } = 0.10f;

        [Export] public float ReadHeight { get; set; } = 0.070f;

        [Export] public Color Face { get; set; } = new Color(0.90f, 0.87f, 0.79f, 0.99f);

        [Export] public Color Lit { get; set; } = new Color(0.98f, 0.95f, 0.85f, 1f);

        [Export] public Color Ink { get; set; } = new Color("#241f18");

        [Export] public int FontSize { get; set; } = 22;

        [Export] public float PixelSize { get; set; } = 0.00028f;

        MeshInstance3D _card;

        QuadMesh _shape;

        StandardMaterial3D _paper;

        Label3D _name;

        Label3D _detail;

        StaticBody3D _touch;

        CollisionShape3D _shell;

        public string Item { get; private set; } = "";

        public bool Turned { get; private set; }

        // developer diagnostics; the words came from the localizer and this is a copy of them
        public string Named { get; private set; } = "";

        public override void _Ready()
        {
            if (_card != null) return;

            _paper = new StandardMaterial3D
            {
                AlbedoColor = Face,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.92f,
            };

            _shape = new QuadMesh { Size = new Vector2(Width, Height) };

            _card = new MeshInstance3D
            {
                Name = "Face",
                Mesh = _shape,
                MaterialOverride = _paper,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            AddChild(_card);

            _name = Written("Name", VerticalAlignment.Top);
            _detail = Written("Detail", VerticalAlignment.Center);

            _detail.Visible = false;

            _touch = new StaticBody3D { Name = "Touch" };

            _shell = new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(Width, Height, 0.003f) },
            };

            _touch.AddChild(_shell);
            AddChild(_touch);
        }

        Label3D Written(string name, VerticalAlignment down)
        {
            var label = new Label3D
            {
                Name = name,
                FontSize = FontSize,
                PixelSize = PixelSize,
                Modulate = Ink,
                Width = Width / PixelSize,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = down,
                Shaded = false,
                Position = new Vector3(0f, 0f, 0.0006f),
            };

            AddChild(label);

            return label;
        }

        public void Deal(string item, string named, string detail)
        {
            _Ready();

            Item = item ?? "";
            Named = named ?? "";

            _name.Text = Named;
            _detail.Text = detail ?? "";

            Turn(false);
        }

        // the same card, read. Nothing appears and nothing is pushed over the scene
        public void Turn(bool over)
        {
            _Ready();

            Turned = over;

            float wide = over ? ReadWidth : Width;
            float tall = over ? ReadHeight : Height;

            _shape.Size = new Vector2(wide, tall);

            if (_shell.Shape is BoxShape3D box) box.Size = new Vector3(wide, tall, 0.003f);

            _name.Width = wide / PixelSize;
            _name.Position = new Vector3(0f, over ? tall * 0.5f - FontSize * PixelSize : 0f, 0.0006f);

            _detail.Visible = over && _detail.Text.Length > 0;
            _detail.Width = wide / PixelSize;
            _detail.Position = new Vector3(0f, -FontSize * PixelSize * 0.4f, 0.0006f);

            Game.Room.TableView.Face(this);
        }

        public bool Owns(GodotObject what) => _touch != null && ReferenceEquals(_touch, what);

        public void Light(bool lit)
        {
            if (_paper == null) return;

            _paper.AlbedoColor = lit ? Lit : Face;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"card: {Item}" + (Turned ? " (turned over)" : "");
    }
}

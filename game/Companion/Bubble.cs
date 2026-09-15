using Godot;

namespace Game.Companion
{
    // A speech bubble at the table. THE_TABLE.md section 6: a thing the player needs is an object,
    // never a panel bolted over the scene - so a line of dialogue is a card that sits on the
    // tabletop beside whoever said it, at the table's own scale and in the table's own light.
    //
    // It holds TEXT, already localized. Nothing here takes a key: GodotLocalizer is the only place
    // a key becomes words, and it has done its work before a bubble is ever asked to show one.
    [GlobalClass]
    public partial class Bubble : Node3D
    {
        // metres. A bubble is a small card lying on a table, not a HUD element
        [Export] public float Width { get; set; } = 0.30f;

        [Export] public int FontSize { get; set; } = 34;

        [Export] public float PixelSize { get; set; } = 0.00042f;

        [Export] public Color Ink { get; set; } = new Color("#1d1a16");

        [Export] public Color Card { get; set; } = new Color(0.94f, 0.92f, 0.86f, 0.96f);

        [Export] public float Fade { get; set; } = 0.22f;

        Label3D _text;

        MeshInstance3D _card;

        StandardMaterial3D _paper;

        // ONE mesh, resized. A fresh QuadMesh per line looks harmless and is not: a companion says
        // thirty lines a fight, the tween holding the old one has not finished, and the resources
        // pile up until Godot counts them out at exit
        QuadMesh _shape;

        double _left;

        Tween _fading;

        // counts the fade down in _Process rather than ending it with a callback. A
        // Callable.From(...) wraps a C# delegate in an object Godot keeps alive on its own side,
        // and one per line said is one per line leaked - measured, not guessed
        double _going;

        public bool Showing => _left > 0.0;

        // what is on it, for a headless check to read back; developer diagnostics, not a second
        // source of truth - the words came from the localizer and this is a copy of them
        public string Said { get; private set; } = "";

        public override void _Ready()
        {
            if (_text != null) return;

            _paper = new StandardMaterial3D
            {
                AlbedoColor = Card,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
                Roughness = 0.9f,
            };

            _shape = new QuadMesh { Size = new Vector2(Width, Width * 0.42f) };

            _card = new MeshInstance3D
            {
                Name = "Card",
                Mesh = _shape,
                MaterialOverride = _paper,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            AddChild(_card);

            _text = new Label3D
            {
                Name = "Text",
                FontSize = FontSize,
                PixelSize = PixelSize,
                Modulate = Ink,
                OutlineSize = 0,
                Width = Width / PixelSize,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Shaded = false,
                NoDepthTest = false,
                Position = new Vector3(0f, 0f, 0.001f),
            };

            AddChild(_text);

            Hide();
            _left = 0.0;
        }

        // the seconds it will stay up, so a caller can sequence two lines without guessing
        public double Say(string text, double seconds = 0.0)
        {
            _Ready();

            if (string.IsNullOrWhiteSpace(text)) { Clear(); return 0.0; }

            Said = text;
            _text.Text = text;

            // the card grows to the line rather than the line being clipped to the card
            float tall = Mathf.Clamp(0.09f + 0.055f * Rows(text), 0.13f, 0.44f);

            _shape.Size = new Vector2(Width, tall);

            _left = seconds > 0.0 ? seconds : Reading.Time(text);

            _fading?.Kill();
            _going = 0.0;
            _paper.AlbedoColor = Card;
            _text.Modulate = Ink;

            Show();

            return _left;
        }

        // roughly, from the label's own wrap width; only the card's height depends on it
        int Rows(string text)
        {
            int perRow = Mathf.Max(8, (int)(Width / (FontSize * PixelSize * 0.52f)));

            return Mathf.CeilToInt(text.Length / (float)perRow);
        }

        public void Clear()
        {
            _fading?.Kill();
            _going = 0.0;
            _left = 0.0;
            Said = "";

            if (_text != null) _text.Text = "";

            Hide();
        }

        public override void _Process(double delta)
        {
            if (_going > 0.0)
            {
                _going -= delta;

                if (_going <= 0.0) { _going = 0.0; Clear(); }

                return;
            }

            if (_left <= 0.0) return;

            _left -= delta;

            if (_left > 0.0) return;

            _left = 0.0;

            Away();
        }

        void Away()
        {
            _fading?.Kill();
            _fading = CreateTween();
            _fading.SetParallel(true);
            _fading.TweenProperty(_paper, "albedo_color:a", 0f, Fade);
            _fading.TweenProperty(_text, "modulate:a", 0f, Fade);

            _going = Fade;
        }

        // a bubble that goes away with the table takes its fade with it; a tween left to finish
        // holds a Callable back to this node, and the node then outlives the scene it was in
        public override void _ExitTree()
        {
            _fading?.Kill();
            _fading = null;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            Showing ? $"bubble: \"{Said}\", {_left:0.0}s left" : "bubble: empty";
    }
}

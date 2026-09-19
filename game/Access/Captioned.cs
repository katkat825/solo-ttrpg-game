using System.Collections.Generic;
using Godot;
using Core.Localization;
using Game.Audio;
using Game.Localization;

namespace Game.Access
{
    // THE SOUNDS, IN WORDS, ON A CARD AT THE NEAR EDGE OF THE TABLE (AX4).
    //
    // Captions, and they are a card lying on the table like everything else that has words on it -
    // not a band across the bottom of the picture, because a band across the bottom of the picture
    // is an HUD and this room has none. It sits nearest the player, where a caption is looked at
    // deliberately rather than caught out of the corner of an eye.
    //
    // IT IS OFF UNTIL ASKED and it stays out of the way when it is on: one caption at a time, and
    // each one leaves on its own, on the same clock the companion's bubbles use - so a player who
    // has turned captions on and the speech speed down gets both.
    //
    // WHO TELLS IT. The things that make noise are told about this card rather than this card
    // hunting them, and Tells() is where that happens: one walk of the tree at boot, from the room,
    // which is the same way the room already finds its furniture. Nothing reaches for a global.
    [GlobalClass]
    public partial class Captioned : Node3D
    {
        [Export] public float Width { get; set; } = 0.26f;

        [Export] public int FontSize { get; set; } = 26;

        [Export] public float PixelSize { get; set; } = 0.00036f;

        [Export] public Color Ink { get; set; } = new Color("#e8e2d2");

        [Export] public Color Card { get; set; } = new Color(0.10f, 0.10f, 0.11f, 0.82f);

        // WHETHER IT IS ON, AND HOW LONG A LINE STAYS. Set by the room out of the settings page;
        // with nothing set there are no captions, which is the state a new player is in
        public Adjustments How { get; set; }

        readonly ILocalizer _text = new GodotLocalizer();

        Label3D _line;

        MeshInstance3D _card;

        StandardMaterial3D _paper;

        QuadMesh _shape;

        double _left;

        // what has been captioned, for a headless check to read back - the same reason the bubble
        // keeps what it said
        readonly List<Sound> _said = new List<Sound>();

        public IReadOnlyList<Sound> Everything => _said;

        public bool Showing => _left > 0.0;

        public string Said { get; private set; } = "";

        public bool On => How?.Captions == true;

        public override void _Ready()
        {
            if (_line != null) return;

            _paper = new StandardMaterial3D
            {
                AlbedoColor = Card,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };

            _shape = new QuadMesh { Size = new Vector2(Width, Width * 0.22f) };

            _card = new MeshInstance3D
            {
                Name = "Card",
                Mesh = _shape,
                MaterialOverride = _paper,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            AddChild(_card);

            _line = new Label3D
            {
                Name = "Text",
                FontSize = FontSize,
                PixelSize = PixelSize,
                Modulate = Ink,
                Width = Width / PixelSize,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Shaded = false,
                Position = new Vector3(0f, 0f, 0.001f),
            };

            AddChild(_line);

            Hide();
        }

        // EVERYTHING IN THE ROOM THAT MAKES A NOISE, TOLD ONCE. Returns how many were told, so a
        // table that has grown a new noisy thing and not been wired to this is visible rather than
        // silently uncaptioned.
        public int Tells(Node root)
        {
            int told = 0;

            foreach (Node node in Everywhere(root ?? this))
            {
                if (node is Game.Dm.DmVoice dm) { dm.Captions = this; told++; }

                if (node is Game.Tray.DiceTray tray) { tray.Captions = this; told++; }
            }

            return told;
        }

        static IEnumerable<Node> Everywhere(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                yield return child;

                foreach (Node under in Everywhere(child)) yield return under;
            }
        }

        // the sound just happened. A caption for a sound nobody asked to see is not drawn and not
        // counted: the count is what the check reads, and it has to mean "shown"
        public bool Says(Sound sound)
        {
            if (!On) return false;

            _Ready();

            string words = _text.Get(sound.CaptionKey());

            if (string.IsNullOrWhiteSpace(words)) return false;

            _said.Add(sound);
            Said = words;

            _line.Text = words;

            float tall = Mathf.Max(0.055f, 0.03f + 0.026f * Rows(words));

            _shape.Size = new Vector2(Width, tall);

            _card.Position = new Vector3(0f, tall * 0.5f, 0f);
            _line.Position = new Vector3(0f, tall * 0.5f, 0.001f);

            _left = How?.Time(words) ?? Game.Companion.Reading.Time(words);

            Show();

            Game.Room.TableView.Face(this);

            return true;
        }

        int Rows(string words)
        {
            int perRow = Mathf.Max(8, (int)(Width / (FontSize * PixelSize * 0.52f)));

            return Mathf.CeilToInt(words.Length / (float)perRow);
        }

        public void Clear()
        {
            _left = 0.0;
            Said = "";

            if (_line != null) _line.Text = "";

            Hide();
        }

        public override void _Process(double delta)
        {
            if (_left <= 0.0) return;

            _left -= delta;

            if (_left <= 0.0) Clear();
        }

        public void Forget() => _said.Clear();

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            !On ? "captions: off"
                : Showing ? $"captions: \"{Said}\", {_left:0.0}s left"
                          : $"captions: on, {_said.Count} shown";
    }
}

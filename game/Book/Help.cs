using Godot;
using Core.Localization;
using Game.Localization;

namespace Game.Book
{
    // THE "?" ON THE TABLE (BK3).
    //
    // The one thing in this game that is unapologetically a button, and it earns it: somebody who
    // cannot find how to ask for help has no way to be told where to look, and hiding the way to the
    // rules inside a diegetic gesture would be the one place cleverness costs more than it buys.
    //
    // What it DOES is still diegetic and is the whole trick: pressing it has the DM hand you the
    // rules book, open at its contents. So there is no help overlay - there is a book, the same book
    // that stands on the bookcase and on the table's shelf, arriving the way anything at this table
    // arrives, in a pair of hands.
    [GlobalClass]
    public partial class Help : Node3D
    {
        [Export] public float Size { get; set; } = 0.034f;

        [Export] public Color Brass { get; set; } = new Color("#8d7b4f");

        [Export] public Color Ink { get; set; } = new Color("#231e17");

        [Export] public int FontSize { get; set; } = 30;

        [Export] public float PixelSize { get; set; } = 0.00036f;

        [Export] public float Lift { get; set; } = 0.25f;

        [Signal] public delegate void AskedEventHandler();

        MeshInstance3D _body;

        StandardMaterial3D _finish;

        StaticBody3D _touch;

        readonly ILocalizer _text = new GodotLocalizer();

        public string Called { get; private set; } = "";

        public override void _Ready()
        {
            if (_body != null) return;

            Called = _text.Get(BookKeys.HelpName);

            _finish = new StandardMaterial3D { AlbedoColor = Brass, Roughness = 0.5f, Metallic = 0.4f };

            _body = new MeshInstance3D
            {
                Name = "Body",
                Mesh = new CylinderMesh
                {
                    TopRadius = Size * 0.5f,
                    BottomRadius = Size * 0.5f,
                    Height = Size * 0.28f,
                },
                MaterialOverride = _finish,
            };

            AddChild(_body);

            // MARKED "?", and the one string in the game that is a glyph rather than a sentence. It
            // is not localized on purpose: "?" is what this button says in every language that uses
            // one, and a translated word here would be a word where a symbol reads faster
            var mark = new Label3D
            {
                Name = "Mark",
                Text = "?",
                FontSize = FontSize,
                PixelSize = PixelSize,
                Modulate = Ink,
                Shaded = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Position = new Vector3(0f, Size * 0.16f, 0f),
                RotationDegrees = new Vector3(-90f, 0f, 0f),
            };

            AddChild(mark);

            _touch = new StaticBody3D { Name = "Touch" };

            // half again as wide as the brass, because the one control a lost player reaches for
            // should not need aiming at - and then held to the floor every reachable thing has
            Span = Game.Access.Hitbox.Around(
                new Vector3(Size * 1.5f, Size * 0.6f, Size * 1.5f));

            _touch.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Span } });

            AddChild(_touch);
        }

        public Vector3 Span { get; private set; }

        public bool Owns(GodotObject what) => _touch != null && ReferenceEquals(_touch, what);

        public Game.Access.Reachable Reach() =>
            new Game.Access.Reachable(Called, Press, _touch, Span, lit: Light);

        public void Light(bool lit)
        {
            if (_finish == null) return;

            _finish.AlbedoColor = lit ? Brass.Lightened(Lift) : Brass;
        }

        public void Press() => EmitSignal(SignalName.Asked);

        // developer only, not localized, never reaches the screen
        public override string ToString() => $"the ? button - \"{Called}\"";
    }
}

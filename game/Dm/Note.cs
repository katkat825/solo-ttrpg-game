using System.Collections.Generic;
using Godot;

namespace Game.Dm
{
    // THE NOTE THE DM SLIDES ACROSS, WITH THE OPTIONS ON IT AS CHECKBOXES.
    //
    // This is the answer to the one question the table had left open: how a choice is offered
    // without a list appearing over the scene. The DM writes the options down, pushes the paper
    // toward you, you tick one, and the paper goes back. It reuses a gesture the hands already
    // have and scales to any number of options, which a row of cards does not.
    //
    // THE GUARDRAIL IS THAT IT LEAVES. A note that stayed up between moments would be a panel
    // with a paper texture on it, and the whole deletion would be undone. It is pushed for the
    // choice, ticked once, and withdrawn - there is no way to ask for it and nothing keeps it.
    //
    // It holds TEXT, already localized, exactly as a bubble does: a key becomes words in one
    // place and it is not here.
    [GlobalClass]
    public partial class Note : Node3D
    {
        [Export] public float Width { get; set; } = 0.30f;

        [Export] public float RowHeight { get; set; } = 0.030f;

        [Export] public float Margin { get; set; } = 0.012f;

        [Export] public int FontSize { get; set; } = 26;

        [Export] public float PixelSize { get; set; } = 0.00036f;

        [Export] public Color Ink { get; set; } = new Color("#241f18");

        [Export] public Color Paper { get; set; } = new Color(0.87f, 0.84f, 0.76f, 0.98f);

        [Export] public Color Lit { get; set; } = new Color(0.96f, 0.93f, 0.83f, 1f);

        // an option whose condition failed is written down and plainly not available, the same
        // call the dialogue cards made: knowing what you cannot do is information
        [Export] public Color Closed { get; set; } = new Color(0.60f, 0.58f, 0.54f, 0.8f);

        [Export] public float BoxSize { get; set; } = 0.011f;

        MeshInstance3D _paper;

        StandardMaterial3D _finish;

        readonly List<Row> _rows = new List<Row>();

        sealed class Row
        {
            public StaticBody3D Touch;
            public MeshInstance3D Tick;
            public Label3D Text;
            public StandardMaterial3D Box;
            public bool Open;
        }

        public bool Showing { get; private set; }

        public int Options => _rows.Count;

        // which row was ticked, or -1; developer diagnostics and what a headless check reads back
        public int Ticked { get; private set; } = -1;

        // the words on it, for a headless check; the localizer produced them and this is a copy
        public IReadOnlyList<string> Written { get; private set; } = new string[0];

        public override void _Ready() => Hide();

        // ONE NOTE AT A TIME. Pushing a second while the first is on the table would leave the
        // player looking at two questions, so the old one is taken back first
        public void Push(IReadOnlyList<string> lines, IReadOnlyList<bool> open = null)
        {
            Withdraw();

            if (lines == null || lines.Count == 0) return;

            float tall = Margin * 2f + RowHeight * lines.Count;

            _finish ??= new StandardMaterial3D
            {
                AlbedoColor = Paper,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.95f,
            };

            if (_paper == null)
            {
                _paper = new MeshInstance3D
                {
                    Name = "Paper",
                    MaterialOverride = _finish,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };

                AddChild(_paper);
            }

            _paper.Mesh = new QuadMesh { Size = new Vector2(Width, tall) };
            _paper.Position = new Vector3(0f, tall * 0.5f, 0f);

            var written = new string[lines.Count];

            for (int at = 0; at < lines.Count; at++)
            {
                bool offered = open == null || at >= open.Count || open[at];

                written[at] = lines[at] ?? "";

                _rows.Add(Write(written[at], offered, at, lines.Count, tall));
            }

            Written = written;
            Ticked = -1;
            Showing = true;

            Show();

            // the same tilt every piece of paper with words on it gets
            Game.Room.TableView.Face(this);
        }

        Row Write(string words, bool offered, int at, int rows, float tall)
        {
            // written top to bottom, which is how somebody writes a list down
            float y = tall - Margin - RowHeight * (at + 0.5f);

            float left = -Width * 0.5f + Margin;

            var box = new StandardMaterial3D
            {
                AlbedoColor = offered ? Paper.Darkened(0.35f) : Closed,
                Roughness = 1f,
            };

            var tick = new MeshInstance3D
            {
                Name = "Box" + at,
                Mesh = new QuadMesh { Size = new Vector2(BoxSize, BoxSize) },
                MaterialOverride = box,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(left + BoxSize * 0.5f, y, 0.0008f),
            };

            AddChild(tick);

            float inset = BoxSize * 2f;

            var text = new Label3D
            {
                Name = "Line" + at,
                Text = words,
                FontSize = FontSize,
                PixelSize = PixelSize,
                Modulate = offered ? Ink : new Color(Ink, 0.5f),
                Width = (Width - Margin * 2f - inset) / PixelSize,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Shaded = false,
                Position = new Vector3(left + inset + (Width - Margin * 2f - inset) * 0.5f, y, 0.0008f),
            };

            AddChild(text);

            var touch = new StaticBody3D { Name = "Row" + at, Position = new Vector3(0f, y, 0f) };

            touch.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(Width, RowHeight, 0.004f) },
            });

            AddChild(touch);

            return new Row { Touch = touch, Tick = tick, Text = text, Box = box, Open = offered };
        }

        // which row a raycast landed on, or -1
        public int RowUnder(GodotObject what)
        {
            for (int at = 0; at < _rows.Count; at++)
                if (ReferenceEquals(_rows[at].Touch, what)) return at;

            return -1;
        }

        public bool IsOpen(int row) => row >= 0 && row < _rows.Count && _rows[row].Open;

        public void Light(int row)
        {
            if (_finish == null) return;

            _finish.AlbedoColor = row >= 0 && IsOpen(row) ? Lit : Paper;
        }

        // ticked, and the mark stays visible for the instant before the paper is taken away -
        // which is what tells you the DM read your answer rather than the note simply vanishing
        public bool Tick(int row)
        {
            if (!IsOpen(row) || Ticked >= 0) return false;

            Ticked = row;

            _rows[row].Box.AlbedoColor = Ink;

            return true;
        }

        public void Withdraw()
        {
            foreach (Row row in _rows)
            {
                row.Touch.QueueFree();
                row.Tick.QueueFree();
                row.Text.QueueFree();
            }

            _rows.Clear();

            Written = new string[0];
            Ticked = -1;
            Showing = false;

            if (_finish != null) _finish.AlbedoColor = Paper;

            Hide();
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            Showing ? $"note: {_rows.Count} option(s)" + (Ticked >= 0 ? $", ticked {Ticked}" : "")
                    : "note: on the pad";
    }
}

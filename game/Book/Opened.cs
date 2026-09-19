using System.Collections.Generic;
using Godot;

namespace Game.Book
{
    // A BOOK LAID OPEN, WITH WORDS ON IT (BK3, BK4).
    //
    // Two pages: the heading on the left, the lines you can touch on the right. That is the whole of
    // it, and it is deliberately the same object whatever is being read - the contents of the
    // campaign book, the story so far, the reload tabs, a chapter of the rules. One open book,
    // re-lettered, rather than a page type per thing to read.
    //
    // THE GUARDRAIL IS THE SAME ONE THE NOTE HAS: it is a book, so it is shut when you are not
    // reading it, and it is shut by closing it rather than by a button somewhere else. A book that
    // stayed open over the table between moments would be a panel with a paper texture on it.
    //
    // It holds TEXT, already localized, exactly as the note and the bubbles do: a key becomes words
    // in one place and it is not here. It knows nothing about which page it is showing either -
    // Opening holds that, and Opening is pure so the part worth getting right can be tested.
    [GlobalClass]
    public partial class Opened : Node3D
    {
        // one page. Two of them side by side is the book
        [Export] public float PageWidth { get; set; } = 0.17f;

        [Export] public float PageHeight { get; set; } = 0.24f;

        [Export] public float RowHeight { get; set; } = 0.026f;

        [Export] public float Margin { get; set; } = 0.014f;

        [Export] public int FontSize { get; set; } = 24;

        [Export] public int HeadingSize { get; set; } = 30;

        [Export] public float PixelSize { get; set; } = 0.00036f;

        [Export] public Color Ink { get; set; } = new Color("#241f18");

        [Export] public Color Paper { get; set; } = new Color("#ddd2b9");

        [Export] public Color Lit { get; set; } = new Color("#f2ead4");

        [Signal] public delegate void TurnedEventHandler(int row);

        // 'Shut' would collide with the Shut() that does it, the way 'Written' would on
        // the sheet: Godot's generator emits an event member per signal
        [Signal] public delegate void ClosedEventHandler();

        MeshInstance3D _left;

        MeshInstance3D _right;

        StandardMaterial3D _paper;

        Label3D _heading;

        readonly List<Row> _rows = new List<Row>();

        sealed class Row
        {
            public StaticBody3D Touch;
            public Label3D Text;
            public bool Turnable;
            public Vector3 Span;
        }

        public bool Showing { get; private set; }

        public int Lines => _rows.Count;

        // the words on it, for a headless check. The localizer produced them and this is a copy
        public IReadOnlyList<string> Written { get; private set; } = new string[0];

        public string Heading { get; private set; } = "";

        public override void _Ready() => Hide();

        // LAY IT OPEN. A heading and some lines; a line that is not turnable is one you read rather
        // than press, which is what a rules chapter and the story so far are made of
        public void Lay(string heading, IReadOnlyList<string> lines,
                        IReadOnlyList<bool> turnable = null)
        {
            Shut(quietly: true);

            _paper ??= new StandardMaterial3D { AlbedoColor = Paper, Roughness = 0.96f };

            _left ??= Page("LeftPage", -PageWidth * 0.5f);
            _right ??= Page("RightPage", PageWidth * 0.5f);

            Heading = heading ?? "";

            if (_heading == null)
            {
                _heading = new Label3D
                {
                    Name = "Heading",
                    FontSize = HeadingSize,
                    PixelSize = PixelSize,
                    Modulate = Ink,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Shaded = false,
                };

                AddChild(_heading);
            }

            _heading.Text = Heading;
            _heading.Width = (PageWidth - Margin * 2f) / PixelSize;
            _heading.Position = new Vector3(-PageWidth + Margin,
                                            PageHeight * 0.5f - Margin, 0.0009f);

            var written = new string[lines?.Count ?? 0];

            for (int at = 0; at < written.Length; at++)
            {
                written[at] = lines[at] ?? "";

                bool press = turnable == null || at >= turnable.Count || turnable[at];

                _rows.Add(Write(written[at], press, at));
            }

            Written = written;
            Showing = true;

            Show();

            // the same tilt every piece of paper with words on it gets
            Game.Room.TableView.Face(this);
        }

        MeshInstance3D Page(string named, float x)
        {
            var page = new MeshInstance3D
            {
                Name = named,
                Mesh = new QuadMesh { Size = new Vector2(PageWidth, PageHeight) },
                MaterialOverride = _paper,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(x, 0f, 0f),
            };

            AddChild(page);

            return page;
        }

        Row Write(string words, bool turnable, int at)
        {
            // written top to bottom, which is how a page reads
            float y = PageHeight * 0.5f - Margin - RowHeight * (at + 0.5f);

            float left = Margin;

            float width = PageWidth - Margin * 2f;

            var text = new Label3D
            {
                Name = "Line" + at,
                Text = words,
                FontSize = FontSize,
                PixelSize = PixelSize,
                Modulate = turnable ? Ink : new Color(Ink, 0.72f),
                Width = width / PixelSize,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Shaded = false,
                Position = new Vector3(left + width * 0.5f, y, 0.0009f),
            };

            AddChild(text);

            var touch = new StaticBody3D
            {
                Name = "Row" + at,
                Position = new Vector3(left + width * 0.5f, y, 0f),
            };

            // the whole line, not the letters: a generous hitbox is an accessibility requirement and
            // not a convenience, and the floor under it is stated once in Hitbox
            Vector3 span = Game.Access.Hitbox.Around(new Vector3(width, RowHeight, 0.004f));

            touch.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = span } });

            AddChild(touch);

            return new Row { Touch = touch, Text = text, Turnable = turnable, Span = span };
        }

        // EVERY LINE ON THE PAGE, AS THINGS THAT CAN BE REACHED (AX1). A line you read rather than
        // press comes back too, and comes back not live: a hand skips it and a screen reader still
        // says it, which is what makes the story so far readable aloud without making it twelve stops
        // on the way to the next page.
        public IEnumerable<Game.Access.Reachable> Rows()
        {
            for (int row = 0; row < _rows.Count; row++)
            {
                int at = row;

                yield return new Game.Access.Reachable(
                    _rows[at].Text?.Text ?? "", () => Turn(at), _rows[at].Touch, _rows[at].Span,
                    _rows[at].Turnable, lit: on => Light(on ? at : -1));
            }
        }

        public int RowUnder(GodotObject what)
        {
            for (int at = 0; at < _rows.Count; at++)
                if (ReferenceEquals(_rows[at].Touch, what)) return at;

            return -1;
        }

        public bool Turnable(int row) => row >= 0 && row < _rows.Count && _rows[row].Turnable;

        public void Light(int row)
        {
            if (_paper == null) return;

            _paper.AlbedoColor = Turnable(row) ? Lit : Paper;
        }

        public bool Turn(int row)
        {
            if (!Turnable(row)) return false;

            EmitSignal(SignalName.Turned, row);

            return true;
        }

        public void Shut(bool quietly = false)
        {
            foreach (Row row in _rows)
            {
                row.Touch.QueueFree();
                row.Text.QueueFree();
            }

            _rows.Clear();

            Written = new string[0];
            Heading = "";

            if (_paper != null) _paper.AlbedoColor = Paper;

            bool was = Showing;

            Showing = false;

            Hide();

            if (was && !quietly) EmitSignal(SignalName.Closed);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            Showing ? $"open at \"{Heading}\", {_rows.Count} line(s)" : "shut";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Sheet;
using Core.Characters;
using Core.Localization;
using Game.Localization;

namespace Game.Sheet
{
    // THE CHARACTER SHEET, ON THE TABLE (R0-R2).
    //
    // A piece of paper with blanks on it. You fill it in by touching the blanks; the DM picks it up
    // and reads it; and from then on everything that happens to you is WRITTEN ON IT, in pencil,
    // while you watch. There is no character screen, no level-up modal and no inventory panel, and
    // that deletion is the point (THE_TABLE.md section 5).
    //
    // The paper is a view. The CHARACTER is Content.Sheet.CharacterSheet, which is Godot-free and
    // is what a save persists - so what is drawn here and what is thrown on the tray cannot drift,
    // because the second is assembled from the first every time.
    [GlobalClass]
    public partial class Sheet : Node3D
    {
        // metres. A sheet of paper on a table, at the table's own scale
        [Export] public float Width { get; set; } = 0.30f;

        [Export] public float Height { get; set; } = 0.40f;

        [Export] public Color Paper { get; set; } = new Color(0.92f, 0.90f, 0.83f);

        [Export] public Color Ink { get; set; } = new Color("#2b2620");

        // pencil, and rubbed out: the marks play makes are a different colour from the printing
        [Export] public Color Pencil { get; set; } = new Color("#4a4740");

        [Export] public Color Smudge { get; set; } = new Color(0.55f, 0.52f, 0.47f, 0.35f);

        [Export] public int FontSize { get; set; } = 26;

        [Export] public float PixelSize { get; set; } = 0.00036f;

        // where the DM's hands can reach it from (R1)
        [Export] public NodePath DmPath { get; set; }

        [Signal] public delegate void FilledEventHandler();

        // 'Written' would collide with the signal member Godot's generator emits for it
        [Signal] public delegate void MarkedEventHandler();

        readonly ILocalizer _text = new GodotLocalizer();

        readonly Dictionary<Line, Label3D> _lines = new Dictionary<Line, Label3D>();

        readonly List<MeshInstance3D> _pips = new List<MeshInstance3D>();

        readonly List<MeshInstance3D> _wear = new List<MeshInstance3D>();

        StaticBody3D _touch;

        Label3D _nerve;

        Label3D _vigor;

        MeshInstance3D _paper;

        Game.Dm.Dm _dm;

        Filling _filling;

        // how many erasures were drawn, so the paper only gains smudges rather than redrawing them
        int _drawn;

        // THE character. Never a copy: what is on the paper is what a save writes
        public CharacterSheet Character { get; private set; } = new CharacterSheet();

        public Filling Offers => _filling;

        public bool Finished => _filling != null && _filling.Finished(Character);

        // the most pips a sheet prints; Nerve caps at 5 (CORE_RULES.md section 7)
        public const int Pips = Core.Combat.Nerve.Cap;

        public override void _Ready()
        {
            if (_paper == null) Draw();

            if (DmPath != null && !DmPath.IsEmpty) _dm = GetNodeOrNull<Game.Dm.Dm>(DmPath);

            Game.Campaigns.Library shelf = Game.Campaigns.Library.Load(quiet: true);

            _filling = new Filling(
                shelf.InPlay.SelectMany(c => c.Classes.All),
                shelf.InPlay.SelectMany(c => c.Sheet.All));

            GD.Print($"sheet   {_filling} - touch a blank to fill it in");

            Redraw();
        }

        // ---- the paper ---------------------------------------------------------------------

        void Draw()
        {
            _paper = new MeshInstance3D
            {
                Name = "Paper",
                Mesh = new QuadMesh { Size = new Vector2(Width, Height) },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = Paper,
                    Roughness = 0.95f,
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            AddChild(_paper);

            AddChild(Printed(SheetKeys.Title, Height * 0.5f - 0.03f, Ink, FontSize + 6));

            float y = Height * 0.5f - 0.08f;

            foreach (Line line in Enum.GetValues<Line>())
            {
                AddChild(Printed(SheetKeys.Label(line), y, Ink, FontSize, -Width * 0.5f + 0.02f,
                                 HorizontalAlignment.Left));

                Label3D written = Written(y, Width * 0.5f - 0.02f);

                _lines[line] = written;

                AddChild(written);

                y -= 0.045f;
            }

            // Vigor, in pencil like everything else play writes: erased and rewritten as you
            // take damage, which is R2's whole demonstration
            y -= 0.02f;

            _vigor = Written(y, Width * 0.5f - 0.02f);

            AddChild(_vigor);

            y -= 0.05f;

            // NERVE AS PIPS (R2). A paper sheet shows a spent-and-regained resource as a row of
            // boxes you tick and erase, the way a real one shows spell slots or saves - and this is
            // the canonical mark the push-or-stop prompt reads, never a separate HUD number.
            _nerve = Printed(SheetKeys.Nerve, y, Ink, FontSize, -Width * 0.5f + 0.02f,
                             HorizontalAlignment.Left);

            AddChild(_nerve);

            for (int at = 0; at < Pips; at++)
            {
                var pip = new MeshInstance3D
                {
                    Name = "Pip" + at,
                    Mesh = new SphereMesh { Radius = 0.006f, Height = 0.012f, RadialSegments = 8, Rings = 4 },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Pencil },
                    Position = new Vector3(Width * 0.5f - 0.02f - at * 0.018f, y, 0.002f),
                };

                _pips.Add(pip);
                AddChild(pip);
            }

            // a thing you touch, so it has a body like the Nerve tokens and the choice cards
            _touch = new StaticBody3D { Name = "Touch" };

            _touch.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(Width, Height, 0.004f) },
            });

            AddChild(_touch);
        }

        Label3D Printed(string key, float y, Color colour, int size,
                        float x = 0f, HorizontalAlignment align = HorizontalAlignment.Center) =>
            new Label3D
            {
                Name = "Print" + key,
                Text = _text.Get(key),
                FontSize = size,
                PixelSize = PixelSize,
                Modulate = colour,
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                Shaded = false,
                Position = new Vector3(x, y, 0.001f),
            };

        Label3D Written(float y, float x) =>
            new Label3D
            {
                FontSize = FontSize,
                PixelSize = PixelSize,
                Modulate = Pencil,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Shaded = false,
                Position = new Vector3(x, y, 0.0012f),
            };


        // ---- filling it in -----------------------------------------------------------------

        // touch a blank and it goes to the next thing installed; no Confirm button anywhere
        public string Touch(Line line)
        {
            if (_filling == null || !line.IsADropdown()) return "";

            string wrote = _filling.Next(Character, line);

            GD.Print($"sheet   {line.Word()}: {(wrote.Length == 0 ? "(blank)" : wrote)}");

            Redraw();

            if (Finished) EmitSignal(SignalName.Filled);

            return wrote;
        }

        // the two lines a person writes rather than picks
        public void WriteIn(Line line, string what)
        {
            if (line.IsADropdown()) return;

            Filling.Write(Character, line, what);

            Redraw();
        }

        // a sheet off a box on the shelf (R4); the paper takes the character, not a copy of it
        public void Take(CharacterSheet character)
        {
            Character = character ?? new CharacterSheet();

            _drawn = 0;

            foreach (MeshInstance3D smudge in _wear) smudge.QueueFree();

            _wear.Clear();

            Redraw();
        }


        // ---- what play writes on it --------------------------------------------------------

        // R2. Damage is erased and rewritten; a level-up writes a die larger; gear gets added. All
        // of it here, on the paper, and nowhere else.
        public void Wrote(Actor hero)
        {
            if (hero == null) return;

            Character.Wrote(hero);

            Redraw();

            EmitSignal(SignalName.Marked);
        }

        public bool Grew(string step)
        {
            if (!Character.Grew(step)) return false;

            GD.Print($"sheet   a new line in the growth column: {step}");

            Redraw();

            return true;
        }

        void Redraw()
        {
            foreach (KeyValuePair<Line, Label3D> line in _lines)
            {
                string id = Filling.On(Character, line.Key);

                line.Value.Text = id.Length == 0
                    ? _text.Get(SheetKeys.Blank)
                    : line.Key.IsADropdown() ? Named(id) : id;
            }

            if (_nerve != null)
                _nerve.Text = _text.Format(SheetKeys.Nerve, Character.Nerve, Pips);

            if (_vigor != null)
                _vigor.Text = Character.Vigor < 0 ? "" : _text.Format(SheetKeys.Vigor, Character.Vigor);

            // ticked and erased, exactly as a paper sheet does it
            for (int at = 0; at < _pips.Count; at++)
                if (_pips[at].MaterialOverride is StandardMaterial3D pip)
                    pip.AlbedoColor = at < Character.Nerve ? Pencil : new Color(Pencil, 0.15f);

            Wear();
        }

        // a class or a race is named by its own key; a pack that has gone shows its id, which is
        // more honest than a blank line where something used to be written
        string Named(string id)
        {
            string key = Core.Localization.KeyConventions.ClassName(id);

            return _text.Has(key) ? _text.Get(key) : id;
        }

        // OVER A CAMPAIGN THE SHEET GETS VISIBLY LIVED IN (R2, THE_TABLE.md section 5). Erasure
        // smudges, one per rubbing-out, scattered where the pencil went. The record of play is the
        // artifact, so this is drawn from the count the sheet itself carries and saved with it.
        void Wear()
        {
            int want = Math.Min(Character.Erasures, MostSmudges);

            if (want <= _drawn) return;

            var where = new RandomNumberGenerator { Seed = (ulong)want };

            for (int at = _drawn; at < want; at++)
            {
                var smudge = new MeshInstance3D
                {
                    Name = "Smudge" + at,
                    Mesh = new QuadMesh { Size = new Vector2(0.03f, 0.012f) },
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = Smudge,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    },
                    Position = new Vector3(where.RandfRange(-Width * 0.4f, Width * 0.4f),
                                           where.RandfRange(-Height * 0.4f, Height * 0.35f),
                                           0.0009f),
                    Rotation = new Vector3(0f, 0f, where.RandfRange(-0.3f, 0.3f)),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };

                _wear.Add(smudge);
                AddChild(smudge);
            }

            _drawn = want;
        }

        // a corner softened from handling is the end of it; past this the paper is grey
        public const int MostSmudges = 40;


        // ---- the DM reads it ----------------------------------------------------------------

        // R1. The beat that establishes who you are playing with better than any intro narration:
        // the hands take the sheet, turn it round, look it over, pause on something, set it down.
        // No text - the gesture carries it, and the pause is most of the gesture.
        public void HandItOver()
        {
            if (_dm == null)
            {
                GD.Print("sheet   nobody across the table to read it");
                return;
            }

            GD.Print("");
            GD.Print($"sheet   handed over - {Character}");

            // gestures, not cues: a cue carries a line, and this beat deliberately has none
            _dm.Does(Reading.ToArray());
        }

        // picked up, turned round, looked over - the pause is the Rest - and set down again. Every
        // one of these is in the closed vocabulary D1 already built; R1 adds no gesture to it.
        public static readonly IReadOnlyList<Content.Encounters.Gesture> Reading =
            new[]
            {
                Content.Encounters.Gesture.ReachBehind,
                Content.Encounters.Gesture.TurnPage,
                Content.Encounters.Gesture.Rest,
                Content.Encounters.Gesture.Withdraw,
            };

        public bool Owns(GodotObject what) => _touch != null && ReferenceEquals(_touch, what);

        // developer only, not localized, never reaches the screen
        public override string ToString() => $"sheet: {Character}";
    }
}

using System.Collections.Generic;
using Godot;
using Core.Characters;
using Core.Localization;

namespace Game.Fight
{
    // ONE ACTOR'S CARD ON THE INITIATIVE STAND.
    //
    // It used to be a name and nothing else, and the player paid for that by stopping mid-fight to
    // read the sheet or to remember a number somebody said aloud four swings ago. A DM keeps the
    // persistent knowns on the card in front of them, so this does too.
    //
    // What is on it, and what deliberately is not:
    //
    //   - YOUR OWN health is a NUMBER. You are allowed to know exactly how you are doing.
    //   - A FOE'S health is one of three WORDS and never a number, never a bar - the pillar is
    //     "watch it get worse", and a gauge turns a fight into arithmetic. The word comes with a
    //     row of notches saying the same thing again in a shape, so nothing here is carried by
    //     colour alone.
    //   - A FOE'S DEFENCE appears once you have beaten it, and then stays. It used to be said out
    //     loud on the throw that revealed it, which wore thin by the third fight.
    //   - CONDITIONS are not here. They are rings on the mini already (ConditionMarks), which is the
    //     other half of the same decision, and a fourth row per card would push the stack out of frame.
    //
    // A row that has nothing to say is not drawn and takes no height, so an untouched foe is a name
    // exactly as before and the stand grows as the fight wears on.
    public partial class Card : Node3D
    {
        // 16 mm of cap height, square-on to the camera; roughly twice what a mini is wide
        public const float NameHeight = 0.016f;

        // the knowns sit under the name in a smaller hand, the way they would be pencilled in
        public const float DetailHeight = 0.011f;

        public const float RowGap = 0.003f;

        // the notches stand between the card and the mat edge, in the margin the marker already uses
        public const float NotchFrom = 0.016f;

        public const float NotchSize = 0.0045f;

        public const float NotchPitch = 0.0062f;

        // HOW WIDE THE CARD IS. A card is a fixed-size object - that is most of what makes it read
        // as a card rather than as words hanging in the air, which is what these were: three
        // Label3Ds and three notches, no paper behind them, floating over the felt at the side of
        // the mat. "The initiative cards aren't cards, they're still floating HUD" was exactly
        // right, and it was right because there was no card.
        public const float Wide = 0.115f;

        // the paper standing a hair behind the writing on it
        public const float Behind = 0.0012f;

        // air above the top row and below the bottom one, so the writing is ON the card
        public const float Padding = 0.005f;

        const int GlyphResolution = 64;

        public Color Ink { get; set; } = new Color(0.72f, 0.66f, 0.54f);

        public Color Up { get; set; } = new Color(1.00f, 0.86f, 0.45f);

        public Color Fallen { get; set; } = new Color(0.38f, 0.34f, 0.30f, 0.55f);

        // the card stock. The same off-white the character sheet is, because they are the same
        // paper on the same table
        public Color Paper { get; set; } = new Color(0.90f, 0.88f, 0.81f, 0.96f);

        public Color Dealt { get; set; } = new Color(0.62f, 0.60f, 0.57f, 0.55f);

        public ILocalizer Text { get; set; }

        public Actor Of { get; private set; }

        // the hero's card keeps a number where a foe's keeps a word
        public bool Yours { get; private set; }

        // how tall this card is with the rows it is currently showing; the stand reads it to stack
        public float Height { get; private set; }

        // THE BOX THE WRITING ON THIS CARD OCCUPIES, in world space, for whoever has to keep it
        // inside the picture.
        //
        // The rows are right-aligned, which puts the card's own origin at the RIGHT-HAND edge of
        // the writing and runs every line leftward from it - away from the mat, and toward the
        // side of the screen. So a long name reaches further off the table than a short one, and
        // how far is a question about a font and a string rather than about this table.
        //
        // MEASURED, NOT MODELLED, and that is the whole of why it is a method here rather than a
        // constant somewhere: the same thing the headless sweep reads. A clamp that measures one
        // box while a check measures another will disagree forever.
        //
        // EMPTY UNTIL THE ROWS HAVE BEEN SHAPED. A Label3D asked for its box in the same frame its
        // text changed answers for the text it had before, so whoever reads this has to be able to
        // tell "nothing yet" from "nothing outside", and an empty box says so.
        public Aabb Written()
        {
            var box = new Aabb();

            bool any = false;

            foreach ((Label3D line, float _) in Rows())
            {
                if (line == null || !line.Visible) continue;

                Aabb one = line.GlobalTransform * line.GetAabb();

                box = any ? box.Merge(one) : one;
                any = true;
            }

            return box;
        }

        // where the turn marker goes: the middle of the name row, in this card's own space
        public float NameRow { get; private set; }

        MeshInstance3D _face;

        QuadMesh _paper;

        Label3D _name;

        Label3D _health;

        Label3D _defence;

        readonly List<MeshInstance3D> _notches = new List<MeshInstance3D>();

        Health _band = Health.Unharmed;

        // -1 until the hero first beats it; a number you have not earned is not on the card
        int _guard = -1;

        int _vigor = int.MinValue;

        bool _acting;

        public void Write(Actor actor, bool yours, ILocalizer text)
        {
            Of = actor;
            Yours = yours;
            Text = text;

            // THE CARD ITSELF, built before the writing so it stands behind it
            _paper = new QuadMesh { Size = new Vector2(Wide, NameHeight + Padding * 2f) };

            _face = new MeshInstance3D
            {
                Name = "Face",
                Mesh = _paper,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = Paper,
                    Roughness = 0.95f,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(-Wide * 0.5f, 0f, -Behind),
            };

            AddChild(_face);

            _name = Row(NameHeight);
            _health = Row(DetailHeight);
            _defence = Row(DetailHeight);

            for (int i = 0; i < 3; i++)
            {
                var notch = new MeshInstance3D
                {
                    Name = "Notch" + (i + 1),
                    Mesh = new BoxMesh { Size = new Vector3(NotchSize, NotchSize, 0.0008f) },
                    MaterialOverride = Unshaded(Ink),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Visible = false,
                    Position = new Vector3(NotchFrom + i * NotchPitch, 0f, 0f),
                };

                AddChild(notch);
                _notches.Add(notch);
            }

            // sentinels, so the first Show writes every row rather than believing it already has.
            // A card is filled in as it is dealt: one that waits for the first frame of the fight
            // is a card that is briefly blank, and check-fight reads it in exactly that gap.
            _vigor = int.MinValue;
            _band = Health.Unharmed;
            _guard = int.MinValue;

            Show(acting: false, knownDefence: -1);
        }

        Label3D Row(float height) => Made(new Label3D
        {
            FontSize = GlyphResolution,
            PixelSize = height / GlyphResolution,

            // the card's own width, so right alignment has an edge to be right against and the
            // writing sits ON the paper rather than wherever the string happened to end
            Width = Wide / (height / GlyphResolution),
            Modulate = Ink,
            OutlineSize = 0,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            DoubleSided = false,
            AlphaCut = Label3D.AlphaCutMode.Discard,

            // right-aligned so the edge against the mat stays straight whatever a name's length
            HorizontalAlignment = HorizontalAlignment.Right,

            // Godot would translate the Label3D itself, a second place a key becomes words
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,

            Visible = false,
        });

        Label3D Made(Label3D line)
        {
            AddChild(line);

            return line;
        }

        // told what the table knows; everything the card shows is derived from here and nothing polls
        public void Show(bool acting, int knownDefence)
        {
            if (Of == null) return;

            _acting = acting;

            Health band = Healths.Of(Of);
            int vigor = Of.Vigor;
            int guard = knownDefence;

            bool changed = band != _band || vigor != _vigor || guard != _guard;

            _band = band;
            _vigor = vigor;
            _guard = guard;

            if (changed) Inscribe();

            Paint();
        }

        // one key per whole line, and the numbers go in as {0} - never a name with a number glued on
        void Inscribe()
        {
            if (_name == null) return;

            _name.Text = Of == null ? ""
                : Text == null ? Of.NameKey
                : Of.Ordinal > 0 ? Text.Format(Of.NameKey, Of.Ordinal)
                : Text.Get(Of.NameKey);

            _name.Visible = true;

            if (Yours)
            {
                _health.Text = Say(CardKeys.Vigor, _vigor);
                _health.Visible = true;

                _defence.Visible = false;

                foreach (MeshInstance3D notch in _notches) notch.Visible = false;
            }
            else
            {
                // a Rabble is unharmed until it is gone - it has no health track to be partway down
                _health.Text = Say(CardKeys.Band(_band));
                _health.Visible = true;

                _defence.Text = _guard >= 0 ? Say(CardKeys.Defence, _guard) : "";
                _defence.Visible = _guard >= 0;

                for (int i = 0; i < _notches.Count; i++)
                    _notches[i].Visible = true;
            }

            Lay();
        }

        string Say(string key, params object[] numbers) =>
            Text == null ? key
          : numbers is { Length: > 0 } ? Text.Format(key, numbers)
          : Text.Get(key);

        // the rows that have something to say, top to bottom, and the card is as tall as they need
        void Lay()
        {
            float total = 0f;
            int rows = 0;

            foreach ((Label3D line, float height) in Rows())
            {
                if (line == null || !line.Visible) continue;

                total += height;
                rows++;
            }

            if (rows > 1) total += (rows - 1) * RowGap;

            Height = total;

            // the card is as tall as what is written on it plus its own margin, and it grows the
            // first time a foe has something new to say - the way a DM's card gains a pencilled line
            if (_paper != null)
            {
                _paper.Size = new Vector2(Wide, total + Padding * 2f);

                _face.Position = new Vector3(-Wide * 0.5f, total * 0.5f, -Behind);
            }

            float top = total;

            foreach ((Label3D line, float height) in Rows())
            {
                if (line == null || !line.Visible) continue;

                line.Position = new Vector3(0f, top - height * 0.5f, 0f);

                if (ReferenceEquals(line, _name)) NameRow = line.Position.Y;

                if (ReferenceEquals(line, _health))
                    foreach (MeshInstance3D notch in _notches)
                        notch.Position = new Vector3(notch.Position.X, line.Position.Y, 0f);

                top -= height + RowGap;
            }
        }

        IEnumerable<(Label3D Line, float Height)> Rows()
        {
            yield return (_name, NameHeight);
            yield return (_health, DetailHeight);
            yield return (_defence, DetailHeight);
        }

        // the acting actor is lit, the fallen are dimmed, and the notches say the band again in a
        // shape so the row is readable with no colour vision at all.
        //
        // The two materials are built once and handed out. Making one per notch per frame is a new
        // Resource sixty times a second per piece in the fight, which is how a table starts
        // stuttering three rounds in.
        void Paint()
        {
            if (_name == null) return;

            // a fallen card is a card turned face down on the table, not one whose writing faded
            if (_face?.MaterialOverride is StandardMaterial3D stock)
                stock.AlbedoColor = Of is { IsDown: true } ? Dealt : Paper;

            Color ink = Of.IsDown ? Fallen : _acting ? Up : Ink;
            int left = Of.IsDown ? 0 : Notches(_band);

            if (ink == _painted && left == _lit) return;

            _painted = ink;
            _lit = left;

            _name.Modulate = ink;
            _health.Modulate = ink;
            _defence.Modulate = ink;

            _cut ??= Unshaded(Colors.White);
            _blank ??= Unshaded(Fallen);

            _cut.AlbedoColor = ink;

            for (int i = 0; i < _notches.Count; i++)
                _notches[i].MaterialOverride = i < left ? _cut : _blank;
        }

        StandardMaterial3D _cut;

        StandardMaterial3D _blank;

        Color _painted = new Color(0f, 0f, 0f, 0f);

        int _lit = -1;

        // three, two, one - the same three bands, counted instead of named
        public static int Notches(Health band) => band switch
        {
            Health.Badly => 1,
            Health.Wounded => 2,
            _ => 3,
        };

        static StandardMaterial3D Unshaded(Color colour) => new StandardMaterial3D
        {
            AlbedoColor = colour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        public void Retranslate() => Inscribe();


        // WHAT THE CARD ACTUALLY READS, for the headless checks. The game never reads these back -
        // a key is never parsed or branched on anywhere - they exist so check-fight
        // can hold the card to the fight instead of to a copy of the fight.

        public string Named => _name == null ? "" : _name.Text;

        public string Says => _health is { Visible: true } ? _health.Text : "";

        public string Guard => _defence is { Visible: true } ? _defence.Text : "";

        public Health Band => _band;

        // the Defence on the card, or -1 while it is still unknown
        public int Known => _guard < 0 ? -1 : _guard;

        // how many notches are cut, the band said again as a count rather than as a word
        public int Cut => _lit < 0 ? 0 : _lit;

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            Of == null ? "a blank card"
          : $"{Of.DebugName}: " + (Yours ? $"vigor {_vigor}" : _band.Word()) +
            (_guard >= 0 ? $", defence {_guard}" : "");
    }
}

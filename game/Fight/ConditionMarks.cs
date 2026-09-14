using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Characters;
using Core.Localization;

namespace Game.Fight
{
    // WHAT IS WRONG WITH THIS PIECE, WRITTEN BESIDE IT (COMBAT_LOOP.md C1).
    //
    // "Conditions read as marks a DM would make, not status icons." So they are words on the mat,
    // in the DM's red pencil, lying flat beside the model and read from the same angle as
    // everything else on the table. Not a bar, not an icon row, not a tooltip. A DM writes
    // "Winded" next to the ogre and everyone at the table knows what that means.
    //
    // THE WORDS COME THROUGH `ILocalizer`, WHICH IS THE WHOLE POINT OF THE MILESTONE. `core/`
    // hands over `Condition.Winded`; `TraitKeys` turns that into `condition.winded.name`; this
    // asks the localizer. No English appears in this file, and pressing L in the tray mangles
    // every word on the mat - which is the only reliable way to find a string somebody hardcoded
    // (CONVENTIONS.md 7). The Label3D's own auto-translation is turned OFF for the same reason it
    // is in `DieMark`: two places where a key becomes text is one place too many, and with the
    // pseudolocale on it would bracket an already bracketed string.
    //
    // WHAT IT DOES NOT DRAW, AND WHY. The milestone asks for the affected die to step down
    // visibly. It does, in the most literal way this game has: the next handful the hero throws
    // is built from the smaller die, so a d8 that became a d6 is physically a smaller solid
    // bouncing on the felt. That is `Actor.BuildPool` reading the trait pipeline, and it needs no
    // drawing at all. A die-shaped token beside the mark would be a second, smaller statement of
    // the same thing, and Phase R's character sheet is where the whole statblock gets a face.
    //
    // A CHILD OF THE PIECE, like the vigor tally, so it goes where the piece goes.
    public partial class ConditionMarks : Node3D
    {
        // a DM's red pencil - the same family as the board's refusal tint, because both are
        // somebody marking the map rather than something standing on it
        public Color Ink { get; set; } = new Color(0.70f, 0.20f, 0.15f);

        // and the flare a mark arrives with. bright for a moment, then it settles into ink -
        // long enough to catch the eye at the far side of the board, short enough that a piece
        // carrying three conditions is not a light show
        public Color Fresh { get; set; } = new Color(1.00f, 0.62f, 0.35f);

        public float FlashSeconds { get; set; } = 0.9f;

        // cap height on the mat, in metres. 7 mm against a 60 mm square - a word written on a map
        // rather than a caption under a photograph
        public const float LineHeight = 0.007f;

        public const float LineGap = 0.0026f;

        // clear of the base on the far side from the vigor tally, so a piece can carry both
        // without either reading as part of the other
        public static readonly Vector3 Beside = new Vector3(-0.024f, 0f, 0.004f);

        public const float Lift = 0.0012f;

        // the glyph atlas the label is rasterised at. same figure DieMark uses, and for the same
        // reason: PixelSize is derived from it so the cap height above is what actually lands
        const int GlyphResolution = 64;

        // injected, so this file cannot start reaching for TranslationServer - the same discipline
        // DieMark and DiceTray hold to
        public ILocalizer Text { get; set; }

        readonly List<Condition> _showing = new List<Condition>();

        readonly Dictionary<Condition, Label3D> _labels = new Dictionary<Condition, Label3D>();

        readonly Dictionary<Condition, float> _flashing = new Dictionary<Condition, float>();

        // FLASHED BEFORE IT EXISTS. The observer fires ConditionApplied from inside
        // CombatEngine.Apply, which is one instruction after the Actor took the Condition and
        // therefore before anything has refreshed the mat. So a flare is remembered here and
        // starts burning the moment its mark is written - otherwise the one event that says
        // "this is NEW" is the one event that always lands on nothing
        readonly HashSet<Condition> _pending = new HashSet<Condition>();

        public IReadOnlyList<Condition> Showing => _showing;

        public Label3D LabelFor(Condition condition) =>
            _labels.TryGetValue(condition, out Label3D label) ? label : null;

        // WHAT THE ACTOR SAYS, and never a copy of it. Called every frame by `Fight`, so a
        // Condition that arrived from anywhere - a vigor threshold, a door forced the hard way, a
        // Trouble - turns up on the mat without its source having to know this exists. The
        // observer's job is only the flare
        public void Show(IReadOnlyList<Condition> conditions)
        {
            if (conditions == null) return;
            if (_showing.SequenceEqual(conditions)) return;

            foreach (Condition gone in _showing.Where(c => !conditions.Contains(c)).ToList())
            {
                if (_labels.TryGetValue(gone, out Label3D label)) label.QueueFree();

                _labels.Remove(gone);
                _flashing.Remove(gone);
                _pending.Remove(gone);
            }

            _showing.Clear();
            _showing.AddRange(conditions);

            for (int i = 0; i < _showing.Count; i++)
            {
                Condition c = _showing[i];

                if (!_labels.ContainsKey(c)) _labels[c] = Write(c);

                // stacked away from the near edge, newest furthest out, so the list grows in one
                // direction and a mark never moves once it has been read
                _labels[c].Position = Beside + new Vector3(0f, Lift, -i * (LineHeight + LineGap));

                if (_pending.Remove(c)) _flashing[c] = FlashSeconds;
            }
        }

        // one mark, in whatever language the table is being played in
        Label3D Write(Condition condition)
        {
            string key = condition.Key();

            var label = new Label3D
            {
                Name = condition.ToString(),

                // deliberately the KEY until the localizer is asked: if the lookup were ever
                // skipped, the mat reads condition.winded.name, which nobody can mistake for a
                // translation
                Text = key,

                FontSize = GlyphResolution,
                PixelSize = LineHeight / GlyphResolution,
                Modulate = Ink,
                OutlineSize = 0,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                DoubleSided = false,
                AlphaCut = Label3D.AlphaCutMode.Discard,
                HorizontalAlignment = HorizontalAlignment.Right,

                // Godot would translate a Label3D itself, which is a second place a key becomes
                // words - see DieMark for the same line and the same reason
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,

                // lying on the mat, glyph tops away from the camera, so it reads upright from the
                // near side of the table
                Transform = new Transform3D(new Basis(Vector3.Right, Vector3.Forward, Vector3.Up), Vector3.Zero),
            };

            AddChild(label);

            if (Text != null) label.Text = Text.Get(key);

            return label;
        }

        // the moment it lands. the observer calls this from ConditionApplied, which is the one
        // thing a per-frame refresh cannot know: that this Condition is NEW rather than merely
        // still true
        public void Flash(Condition condition)
        {
            if (_labels.ContainsKey(condition)) _flashing[condition] = FlashSeconds;
            else _pending.Add(condition);
        }

        public override void _Process(double delta)
        {
            if (_flashing.Count == 0) return;

            foreach (Condition c in _flashing.Keys.ToList())
            {
                float left = _flashing[c] - (float)delta;

                if (!_labels.TryGetValue(c, out Label3D label))
                {
                    _flashing.Remove(c);
                    continue;
                }

                if (left <= 0f)
                {
                    label.Modulate = Ink;
                    _flashing.Remove(c);
                    continue;
                }

                _flashing[c] = left;
                label.Modulate = Ink.Lerp(Fresh, left / Mathf.Max(FlashSeconds, 0.001f));
            }
        }

        // switching language, or turning the pseudolocale on, rewrites the mat without anything
        // being re-thrown or re-fought - the same behaviour the tray's marks have
        public override void _Notification(int what)
        {
            if (what == NotificationTranslationChanged) Retranslate();
        }

        public void Retranslate()
        {
            if (Text == null) return;

            foreach (KeyValuePair<Condition, Label3D> mark in _labels)
                mark.Value.Text = Text.Get(mark.Key.Key());
        }

        // DEVELOPER ONLY - not localized, never reaches the screen
        public override string ToString() =>
            _showing.Count == 0 ? "no marks" : string.Join(", ", _showing);
    }
}

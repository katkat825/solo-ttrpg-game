using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Characters;
using Core.Localization;

namespace Game.Fight
{
    public partial class ConditionMarks : Node3D
    {
        public Color Ink { get; set; } = new Color(0.70f, 0.20f, 0.15f);

        public Color Fresh { get; set; } = new Color(1.00f, 0.62f, 0.35f);

        public float FlashSeconds { get; set; } = 0.9f;

        // cap height in metres: 7 mm against a 60 mm square
        public const float LineHeight = 0.007f;

        public const float LineGap = 0.0026f;

        // far side from the vigor tally, so a piece can carry both
        public static readonly Vector3 Beside = new Vector3(-0.024f, 0f, 0.004f);

        public const float Lift = 0.0012f;

        // PixelSize is derived from this, so the cap height above is what lands
        const int GlyphResolution = 64;

        // injected, so this file never reaches for TranslationServer
        public ILocalizer Text { get; set; }

        readonly List<Condition> _showing = new List<Condition>();

        readonly Dictionary<Condition, Label3D> _labels = new Dictionary<Condition, Label3D>();

        readonly Dictionary<Condition, float> _flashing = new Dictionary<Condition, float>();

        // a flash can arrive before its mark exists, so it is remembered until Show writes it
        readonly HashSet<Condition> _pending = new HashSet<Condition>();

        public IReadOnlyList<Condition> Showing => _showing;

        public Label3D LabelFor(Condition condition) =>
            _labels.TryGetValue(condition, out Label3D label) ? label : null;

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

                // newest furthest out, so a mark never moves once it has been read
                _labels[c].Position = Beside + new Vector3(0f, Lift, -i * (LineHeight + LineGap));

                if (_pending.Remove(c)) _flashing[c] = FlashSeconds;
            }
        }

        Label3D Write(Condition condition)
        {
            string key = condition.Key();

            var label = new Label3D
            {
                Name = condition.ToString(),

                // deliberately the key until the localizer is asked, so a skipped lookup shows condition.winded.name
                Text = key,

                FontSize = GlyphResolution,
                PixelSize = LineHeight / GlyphResolution,
                Modulate = Ink,
                OutlineSize = 0,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                DoubleSided = false,
                AlphaCut = Label3D.AlphaCutMode.Discard,
                HorizontalAlignment = HorizontalAlignment.Right,

                // Godot would translate the Label3D itself, a second place a key becomes words
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,

                // lying on the mat, glyph tops away from camera, upright from the near side
                Transform = new Transform3D(new Basis(Vector3.Right, Vector3.Forward, Vector3.Up), Vector3.Zero),
            };

            AddChild(label);

            if (Text != null) label.Text = Text.Get(key);

            return label;
        }

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

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            _showing.Count == 0 ? "no marks" : string.Join(", ", _showing);
    }
}

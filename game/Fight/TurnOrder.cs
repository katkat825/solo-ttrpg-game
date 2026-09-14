using System.Collections.Generic;
using Godot;
using Core.Characters;
using Core.Localization;
using Game.Board;

namespace Game.Fight
{
    // THE ORDER, WRITTEN DOWN THE SIDE OF THE MAP (COMBAT_LOOP.md C3).
    //
    // Initiative is rolled once, at the top, and never again (CORE_RULES.md section 8) - which
    // means the order is a thing that sits there for the whole fight rather than a thing that
    // happens. So it is written down, in the margin beside the mat, the way a DM writes it on the
    // corner of the battle map: a short list, top to bottom, with a mark against whoever is up.
    //
    // NOT A UI PANEL. THE_TABLE.md 6 - the room is the entire UI. This is on the table, lying flat
    // on the wood beside the board, read from the same angle as everything else and lit by the
    // same lamp. There is no overlay, no corner widget and no screen space anywhere in it.
    //
    // EVERY NAME IS A KEY. `Actor.NameKey` gives `actor.rabble.name_numbered` for a numbered foe
    // and `actor.rabble.name` for a lone one, and the ordinal goes in as {0} through
    // `ILocalizer.Format` - never glued on with a space, because "Rabble 3" is one sentence in
    // English and is not one in every language (CONVENTIONS.md 7). This is the first place in the
    // game where that rule has an actual consumer.
    public partial class TurnOrder : Node3D
    {
        // how far off the edge of the mat the list is written, in metres. Clear of the board and
        // well inside the camera's frame at the table's fixed 60 degrees
        public const float Margin = 0.06f;

        public const float LineHeight = 0.008f;

        public const float LineGap = 0.004f;

        public const float Lift = 0.002f;

        // and how far the mark against the current name sticks out past it
        public const float MarkerGap = 0.006f;

        public const float MarkerSize = 0.004f;

        // ink for somebody still standing, for whoever is up, and for the fallen. Three states and
        // no more: a list that colour-codes tiers is a list you have to learn
        public Color Ink { get; set; } = new Color(0.72f, 0.66f, 0.54f);

        public Color Up { get; set; } = new Color(1.00f, 0.86f, 0.45f);

        public Color Fallen { get; set; } = new Color(0.38f, 0.34f, 0.30f, 0.55f);

        public ILocalizer Text { get; set; }

        readonly List<Actor> _order = new List<Actor>();

        readonly Dictionary<Actor, Label3D> _lines = new Dictionary<Actor, Label3D>();

        MeshInstance3D _marker;

        Actor _up;

        const int GlyphResolution = 64;

        public IReadOnlyList<Actor> Listed => _order;

        public Label3D LineFor(Actor actor) =>
            actor != null && _lines.TryGetValue(actor, out Label3D line) ? line : null;

        // written once, when the fight begins, because the order is decided once. It is rewritten
        // only if somebody joins the fight, which nothing can do yet
        public void Write(IReadOnlyList<Actor> order, BoardMetrics metrics)
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }

            _lines.Clear();
            _order.Clear();
            _marker = null;

            if (order == null || metrics == null) return;

            _order.AddRange(order);

            // hard against the left edge of the mat and running away from the near side of the
            // table, so the list reads top-to-bottom from the player's seat
            float x = -metrics.HalfWidth - Margin;
            float z = -metrics.HalfDepth;

            for (int i = 0; i < _order.Count; i++)
            {
                Label3D line = Write(_order[i]);
                line.Position = new Vector3(x, Lift, z + i * (LineHeight + LineGap));
                _lines[_order[i]] = line;
            }

            AddChild(_marker = new MeshInstance3D
            {
                Name = "Marker",
                Mesh = new BoxMesh { Size = new Vector3(MarkerSize, 0.0008f, MarkerSize) },
                MaterialOverride = Unshaded(Up),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            });
        }

        Label3D Write(Actor actor)
        {
            var line = new Label3D
            {
                Name = actor.DebugName,

                // the key until the localizer is asked, so a lookup that never happened reads as
                // actor.rabble.name_numbered rather than as a blank margin
                Text = actor.NameKey,

                FontSize = GlyphResolution,
                PixelSize = LineHeight / GlyphResolution,
                Modulate = Ink,
                OutlineSize = 0,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                DoubleSided = false,
                AlphaCut = Label3D.AlphaCutMode.Discard,

                // right-aligned, so the list grows away from the mat and its edge stays straight
                // however long a translated name turns out to be
                HorizontalAlignment = HorizontalAlignment.Right,

                // Godot would translate a Label3D itself, which is a second place a key becomes
                // words - see DieMark for the same line and the same reason
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,

                // lying on the table, glyph tops away from the camera
                Transform = new Transform3D(new Basis(Vector3.Right, Vector3.Forward, Vector3.Up), Vector3.Zero),
            };

            AddChild(line);

            Inscribe(actor, line);

            return line;
        }

        // ONE KEY, ONE WHOLE NAME. A numbered foe's key takes the ordinal as {0}; a lone one's
        // does not take anything. Never `name + " " + n`
        void Inscribe(Actor actor, Label3D line)
        {
            if (Text == null) return;

            line.Text = actor.Ordinal > 0
                ? Text.Format(actor.NameKey, actor.Ordinal)
                : Text.Get(actor.NameKey);
        }

        // whose turn it is now, and who has stopped having turns. Called every frame by `Fight`,
        // like the tallies and the marks, so the margin is a view of the fight and never a copy
        public void Mark(Actor acting)
        {
            _up = acting;

            foreach (KeyValuePair<Actor, Label3D> line in _lines)
                line.Value.Modulate = line.Key.IsDown ? Fallen
                    : ReferenceEquals(line.Key, acting) ? Up
                    : Ink;

            if (_marker == null) return;

            if (acting == null || !_lines.TryGetValue(acting, out Label3D at))
            {
                _marker.Visible = false;
                return;
            }

            // just past the end of the name, on the side away from the mat, so it never lands on
            // top of a word
            _marker.Visible = true;
            _marker.Position = at.Position + new Vector3(MarkerGap, 0f, 0f);
        }

        static StandardMaterial3D Unshaded(Color colour) => new StandardMaterial3D
        {
            AlbedoColor = colour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        // switching language rewrites the margin without re-rolling the order - the same behaviour
        // the tray's marks and the condition marks have
        public override void _Notification(int what)
        {
            if (what == NotificationTranslationChanged) Retranslate();
        }

        public void Retranslate()
        {
            foreach (KeyValuePair<Actor, Label3D> line in _lines) Inscribe(line.Key, line.Value);
        }

        // DEVELOPER ONLY - not localized, never reaches the screen
        public override string ToString() =>
            _order.Count == 0 ? "no order yet" : string.Join(" > ", _order.ConvertAll(a => a.DebugName));
    }
}

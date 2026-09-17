using System.Collections.Generic;
using Godot;
using Core.Characters;
using Core.Localization;
using Game.Board;

namespace Game.Fight
{
    public partial class TurnOrder : Node3D
    {
        // THE INITIATIVE LIST, AS A THING ON THE TABLE.
        //
        // It used to lie FLAT against the felt in 8 mm type. From the fixed camera - 60 degrees
        // down, a metre and a half away - that is nine pixels of text squashed to half height, and
        // the eye check said what you would expect: "there are words next to the map on the upper
        // left. I can't read them."
        //
        // So it stands up instead, the way a DM's initiative card stands at the side of the mat:
        // one small card at the far corner of the left edge, tilted toward the player, with the
        // names stacked UP it rather than down the table. It is still an object and not a panel -
        // THE_TABLE.md section 6 is a hard rule - it is just an object you can read.

        // off the mat edge, clear of the board and inside the camera frame
        public const float Margin = 0.055f;

        // 16 mm of cap height, square-on to the camera; roughly twice what a mini is wide
        public const float LineHeight = 0.016f;

        public const float LineGap = 0.006f;

        public const float Lift = 0.004f;

        public const float MarkerGap = 0.010f;

        public const float MarkerSize = 0.007f;

        // where the card stands along the mat's left edge: the FAR corner, because the near one is
        // where the companion sits with its chin on the map (table.tscn places it there)
        public const float FromTheFarEdge = 0.02f;

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

            // the card stands at the far corner of the mat's left edge, and the names stack up
            // it from the table, so the one at the top of the list is the one highest off the felt
            Position = new Vector3(-metrics.HalfWidth - Margin, Lift,
                                   -metrics.HalfDepth + FromTheFarEdge);

            for (int i = 0; i < _order.Count; i++)
            {
                Label3D line = Write(_order[i]);

                line.Position = new Vector3(0f, (_order.Count - 1 - i) * (LineHeight + LineGap), 0f);

                _lines[_order[i]] = line;
            }

            // tilted toward the player, like every other thing on this table with words on it
            Game.Room.TableView.Face(this);

            AddChild(_marker = new MeshInstance3D
            {
                Name = "Marker",
                // standing with the list now, so it is a square pip beside the name and not a
                // tile lying on the felt
                Mesh = new BoxMesh { Size = new Vector3(MarkerSize, MarkerSize, 0.0008f) },
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

                // the key until the localizer is asked, so a skipped lookup shows the key not a blank
                Text = actor.NameKey,

                FontSize = GlyphResolution,
                PixelSize = LineHeight / GlyphResolution,
                Modulate = Ink,
                OutlineSize = 0,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                DoubleSided = false,
                AlphaCut = Label3D.AlphaCutMode.Discard,

                // right-aligned so the edge stays straight whatever a translated name's length
                HorizontalAlignment = HorizontalAlignment.Right,

                // Godot would translate the Label3D itself, a second place a key becomes words
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            };

            AddChild(line);

            Inscribe(actor, line);

            return line;
        }

        // numbered key takes the ordinal as {0}; never name + space + n
        void Inscribe(Actor actor, Label3D line)
        {
            if (Text == null) return;

            line.Text = actor.Ordinal > 0
                ? Text.Format(actor.NameKey, actor.Ordinal)
                : Text.Get(actor.NameKey);
        }

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

            _marker.Visible = true;
            _marker.Position = at.Position + new Vector3(MarkerGap, LineHeight * 0.4f, 0f);
        }

        static StandardMaterial3D Unshaded(Color colour) => new StandardMaterial3D
        {
            AlbedoColor = colour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        public override void _Notification(int what)
        {
            if (what == NotificationTranslationChanged) Retranslate();
        }

        public void Retranslate()
        {
            foreach (KeyValuePair<Actor, Label3D> line in _lines) Inscribe(line.Key, line.Value);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            _order.Count == 0 ? "no order yet" : string.Join(" > ", _order.ConvertAll(a => a.DebugName));
    }
}

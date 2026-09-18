using System.Collections.Generic;
using Godot;
using Core.Characters;
using Core.Localization;
using Game.Board;

namespace Game.Fight
{
    public partial class TurnOrder : Node3D
    {
        // THE INITIATIVE STAND, AS A THING ON THE TABLE.
        //
        // It used to lie FLAT against the felt in 8 mm type. From the fixed camera - 60 degrees
        // down, a metre and a half away - that is nine pixels of text squashed to half height, and
        // the eye check said what you would expect: "there are words next to the map on the upper
        // left. I can't read them."
        //
        // So it stands up instead, the way a DM's initiative cards stand at the side of the mat:
        // one small stack at the far corner of the left edge, tilted toward the player, growing UP
        // it rather than down the table. It is still an object and not a panel - THE_TABLE.md
        // section 6 is a hard rule - it is just an object you can read.
        //
        // What stands here is a stack of CARDS now rather than a list of names, and what each one
        // carries is Card's business. This file owns the stack: who is in it, how tall each card
        // came out, and which one has the turn.

        // off the mat edge, clear of the board and inside the camera frame
        public const float Margin = 0.055f;

        // one card's worth of air before the next one, so a stack reads as cards and not as a list
        public const float CardGap = 0.006f;

        public const float Lift = 0.004f;

        public const float MarkerGap = 0.010f;

        public const float MarkerSize = 0.007f;

        // where the stack stands along the mat's left edge: the FAR corner, because the near one is
        // where the companion sits with its chin on the map (table.tscn places it there)
        public const float FromTheFarEdge = 0.02f;

        public Color Ink { get; set; } = new Color(0.72f, 0.66f, 0.54f);

        public Color Up { get; set; } = new Color(1.00f, 0.86f, 0.45f);

        public Color Fallen { get; set; } = new Color(0.38f, 0.34f, 0.30f, 0.55f);

        public ILocalizer Text { get; set; }

        readonly List<Actor> _order = new List<Actor>();

        readonly Dictionary<Actor, Card> _cards = new Dictionary<Actor, Card>();

        // a Defence you have beaten, and therefore know. Nothing else puts a number on a foe's card
        readonly Dictionary<Actor, int> _guards = new Dictionary<Actor, int>();

        MeshInstance3D _marker;

        public IReadOnlyList<Actor> Listed => _order;

        public Card CardFor(Actor actor) =>
            actor != null && _cards.TryGetValue(actor, out Card card) ? card : null;

        // whether this foe's Defence is on its card yet; the fight decides when, this remembers
        public bool Knows(Actor actor) => actor != null && _guards.ContainsKey(actor);

        public void Write(IReadOnlyList<Actor> order, BoardMetrics metrics, Actor yours = null)
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }

            _cards.Clear();
            _order.Clear();
            _guards.Clear();
            _marker = null;

            if (order == null || metrics == null) return;

            _order.AddRange(order);

            // the stack stands at the far corner of the mat's left edge and grows up from the
            // table, so the one at the top of the order is the one highest off the felt
            Position = new Vector3(-metrics.HalfWidth - Margin, Lift,
                                   -metrics.HalfDepth + FromTheFarEdge);

            foreach (Actor actor in _order)
            {
                var card = new Card
                {
                    Name = actor.DebugName,
                    Ink = Ink,
                    Up = Up,
                    Fallen = Fallen,
                };

                AddChild(card);

                card.Write(actor, ReferenceEquals(actor, yours), Text);

                _cards[actor] = card;
            }

            Stack();

            // tilted toward the player, like every other thing on this table with words on it
            Game.Room.TableView.Face(this);

            AddChild(_marker = new MeshInstance3D
            {
                Name = "Marker",
                // standing with the stack now, so it is a square pip beside the name and not a
                // tile lying on the felt
                Mesh = new BoxMesh { Size = new Vector3(MarkerSize, MarkerSize, 0.0008f) },
                MaterialOverride = Unshaded(Up),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            });
        }

        // the first throw that clears a foe's guard puts the number on its card, and there it stays
        public bool Reveal(Actor foe, int defence)
        {
            if (foe == null || _guards.ContainsKey(foe)) return false;

            _guards[foe] = defence;

            return true;
        }

        // told every frame, like the pips and the marks: state changed outside the fight still shows
        public void Mark(Actor acting)
        {
            float was = _height;

            foreach (KeyValuePair<Actor, Card> card in _cards)
                card.Value.Show(ReferenceEquals(card.Key, acting),
                                _guards.TryGetValue(card.Key, out int guard) ? guard : -1);

            // a card grows the first time it has something new to say; re-stack only then
            if (!Mathf.IsEqualApprox(was, Measured())) Stack();

            if (_marker == null) return;

            Card at = CardFor(acting);

            if (acting == null || at == null)
            {
                _marker.Visible = false;
                return;
            }

            _marker.Visible = true;
            _marker.Position = at.Position + new Vector3(MarkerGap, at.NameRow, 0f);
        }

        float _height;

        float Measured()
        {
            float total = 0f;

            foreach (Actor actor in _order)
                if (_cards.TryGetValue(actor, out Card card)) total += card.Height;

            return total;
        }

        // the last in the order stands lowest, so the top of the stack is whose turn comes first
        void Stack()
        {
            float up = 0f;

            for (int i = _order.Count - 1; i >= 0; i--)
            {
                if (!_cards.TryGetValue(_order[i], out Card card)) continue;

                card.Position = new Vector3(0f, up, 0f);

                up += card.Height + CardGap;
            }

            _height = Measured();
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
            foreach (KeyValuePair<Actor, Card> card in _cards) card.Value.Retranslate();

            Stack();
        }

        // developer only, not localized, never reaches the screen
        // the height is here because it is the one number that can put this off the top of the
        // picture: cards grow as the fight wears on, and the eye check has caught the initiative
        // list being unreadable once already
        public override string ToString() =>
            _order.Count == 0
                ? "no order yet"
                : string.Join(" > ", _order.ConvertAll(a => a.DebugName)) +
                  $" ({_height * 1000f:0} mm of cards)";
    }
}

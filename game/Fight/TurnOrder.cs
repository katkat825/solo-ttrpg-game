using System.Collections.Generic;
using Godot;
using Core.Characters;
using Core.Localization;
using Game.Board;
using Game.Room;

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

        // THE MARKER IS THE END-TURN GESTURE (V2). It used to be tapping your own mini, which is a
        // piece on a map saying "the thing I am standing on is the control" - and it collided with
        // the one gesture a mini actually wants, which is going somewhere. A DM ends a turn by
        // moving the marker down the initiative stack, so you do.
        //
        // It is 7 mm of pip and a fingertip is 24, so the body it is touched by is Hitbox's floor
        // and not the pip's own size.
        public static readonly Vector3 MarkerReach =
            Game.Access.Hitbox.Around(new Vector3(MarkerSize, MarkerSize, 0.0008f));

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
            _nudge = null;
            _pip = null;
            Marking = null;

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

            _shoved = 0f;
            Settled = false;
            _measured = default;

            KeepInThePicture();

            AddChild(_marker = new MeshInstance3D
            {
                Name = "Marker",
                // standing with the stack now, so it is a square pip beside the name and not a
                // tile lying on the felt
                Mesh = new BoxMesh { Size = new Vector3(MarkerSize, MarkerSize, 0.0008f) },
                MaterialOverride = _pip = Unshaded(Up),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            });

            // its own body, so a nudge at the marker is not read as a click on whatever is behind it
            _marker.AddChild(_nudge = new StaticBody3D { Name = "Nudge" });

            _nudge.AddChild(new CollisionShape3D
            {
                Name = "Reach",
                Shape = new BoxShape3D { Size = MarkerReach },
            });
        }

        // THE STACK STANDS OFF THE MAT'S EDGE, AND THE MAT IS NOT ALWAYS INSIDE THE PICTURE.
        //
        // Where it goes is the mat's own half-width plus a margin, which is right for the shipped
        // 8x8 room and wrong the moment a campaign ships a wider one: a mat wide enough to fill the
        // frame puts its own left edge at the edge of the picture, and the turn order stands
        // OUTSIDE that edge. Every name in the ashfall fight was off the screen, by up to 67 mm,
        // and no map has to be malformed for it - it is the ordinary consequence of a bigger room,
        // and a Workshop campaign will ship one. A name is a player's own string too, so how far a
        // card reaches is not a number this table can know in advance.
        //
        // So the stack is pulled back in until it fits, from the rows' own measured boxes, every
        // frame the cards are told what to say. It hugs the mat wherever there is room, which is
        // every map small enough, and slides over the felt's edge on the ones that are not -
        // because a turn order you cannot see is worth less than one an inch onto the mat.
        //
        // ASKED EVERY FRAME RATHER THAN ONCE, because a Label3D answers for the text it had before
        // the one it was just given: measured at the moment a card is written, half of these boxes
        // are a frame out of date. Asked again next frame it settles, and it early-outs the moment
        // everything fits, which is every frame after the first.
        void KeepInThePicture()
        {
            Framing frame = Framing.Of(IsInsideTree() ? GetViewport()?.GetCamera3D() : null);

            if (!frame.Exists) return;

            Aabb all = default;
            bool any = false;

            foreach (Card card in _cards.Values)
            {
                Aabb written = card.Written();

                // a card whose rows have not been shaped yet has nothing to say about where it is
                if (written.Size == Vector3.Zero) continue;

                all = any ? all.Merge(written) : written;
                any = true;
            }

            if (!any) return;

            // TWO FRAMES HAVE TO AGREE BEFORE THIS IS BELIEVED. A row that has not been shaped yet
            // does not measure as nothing - it measures as a sliver at the card's own origin, which
            // fits the picture beautifully and is not where the words are going to be. One frame's
            // answer said the stack was fine while every name in it was off the screen, so a
            // measurement counts only once the one before it said the same thing.
            bool same = _measured.Size.IsEqualApprox(all.Size) &&
                        _measured.Position.IsEqualApprox(all.Position);

            _measured = all;

            float worst = frame.Margin(all.GetCenter(), all.Size);

            if (worst >= 0f) { Settled = same; return; }

            Settled = false;

            // in from whichever side it fell off, which is the left on every map so far. The
            // camera does not yaw, so one step is exact, and the next frame confirms it against
            // boxes that have caught up
            float over = Mathf.Min(-worst, Furthest - _shoved);

            if (over <= 0f) { Settled = same; return; }

            Position += new Vector3(GlobalPosition.X < 0f ? over : -over, 0f, 0f);

            _shoved += over;

            // ONTO THE MAT IS FINE AND RUNNING OUT OF MAT IS NOT. A stack standing on the felt at
            // the far corner is what a real table looks like; one that would keep sliding is on
            // its way over the board, where the marker's own body would start eating clicks meant
            // for a square. So it stops at Furthest, and only then says anything - the shipped
            // 12-wide yard needs 106 mm of this and is not a problem to be told about.
            if (_shoved < Furthest) return;

            GD.PushWarning($"turn order: the stack has been pulled {_shoved * 1000f:0} mm onto " +
                           "the mat and is STILL not all in the picture - this map is wider than " +
                           "the camera, or something on it is named at length");
        }

        // IS THE STACK DONE MOVING. A Label3D answers for the text it had before the one it was
        // just given, so the first frame's boxes are a frame out of date and the stack is still
        // being pulled in while they catch up. Nothing should be measured against it until this
        // is true - the same discipline as not standing a piece on a mat that is in the air.
        public bool Settled { get; private set; }

        // how far this stack has been pulled in from where the mat put it, so the warning is about
        // the whole slide rather than the last millimetre of it
        float _shoved;

        // the whole stack's box as the last frame measured it
        Aabb _measured;

        // it never slides further onto the felt than this, whatever the map or the names: about
        // two and a half squares at the mat's far corner, which is where a DM's cards would lie
        const float Furthest = 0.16f;

        StaticBody3D _nudge;

        StandardMaterial3D _pip;

        // whose turn the marker is standing beside, which is the only turn it can end
        public Actor Marking { get; private set; }

        public bool Owns(GodotObject collider) =>
            _nudge != null && collider != null && ReferenceEquals(_nudge, collider);

        // under your hand, or under the cursor: the pip brightens, which is the affordance the
        // object has rather than a rectangle drawn around it
        public void Light(bool lit)
        {
            if (_pip != null) _pip.AlbedoColor = lit ? Up.Lightened(0.45f) : Up;
        }

        // WHAT IT IS, for the hand that reaches with no mouse and the voice that reads it out.
        // One description, so a keyboard, a cursor and a screen reader cannot disagree about what
        // this is or what pressing it does.
        public Game.Access.Reachable Reach(ILocalizer text, System.Action press) =>
            new Game.Access.Reachable(
                text == null ? TurnKeys.EndTurn : text.Get(TurnKeys.EndTurn),
                press, _nudge, MarkerReach, live: _marker is { Visible: true }, lit: Light);

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

            KeepInThePicture();

            if (_marker == null) return;

            Card at = CardFor(acting);

            if (acting == null || at == null)
            {
                Marking = null;
                _marker.Visible = false;
                return;
            }

            Marking = acting;
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

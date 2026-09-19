using System.Collections.Generic;
using Godot;
using Core.Localization;
using Game.Dialogue;
using Game.Localization;

namespace Game.Explore
{
    // HOW YOU ANSWER A MOMENT, AS OBJECTS.
    //
    // Offer decides what is on the table and in what form; this puts it there and passes the click
    // back. Two forms and no third: cards laid out in front of you for the things you could do to
    // what is standing there, and the DM's note for a set of options somebody wrote down.
    //
    // Nothing here stands between moments. An offer is made, answered, and swept - which is the
    // difference between a table and a screen with paper on it.
    [GlobalClass]
    public partial class Response : Node3D
    {
        // whose hands push and take back the note; without one the note is not offered at all,
        // because a note that arrives by itself is a panel
        [Export] public NodePath DmPath { get; set; }

        [Export] public Vector3 CardsAt { get; set; } = new Vector3(0f, 0.002f, 0.20f);

        [Export] public float CardGap { get; set; } = 0.058f;

        // which answer was taken, by its index in the offer that was made
        [Signal] public delegate void AnsweredEventHandler(int index);

        Game.Dm.Dm _dm;

        readonly List<ChoiceCard> _cards = new List<ChoiceCard>();

        readonly ILocalizer _text = new GodotLocalizer();

        int _lit = -1;

        public Offer Offered { get; private set; } = Offer.Nothing;

        public bool Asking => Offered.Any;

        // WHAT WAS TAKEN, kept after the offer is swept. The answer arrives as an index and the
        // cards carrying it are gone by then, so the one that was ticked is held here rather than
        // every listener keeping its own copy of what it asked
        public Answer Taken { get; private set; }

        public override void _Ready()
        {
            if (DmPath != null && !DmPath.IsEmpty) _dm = GetNodeOrNull<Game.Dm.Dm>(DmPath);
        }

        // the words on each answer, resolved once. A headless check reads this back, and it is a
        // copy of what the localizer said rather than a second source of them
        public IReadOnlyList<string> Words { get; private set; } = new string[0];

        public bool Ask(Offer offer)
        {
            Sweep();

            Offered = offer ?? Offer.Nothing;
            Taken = null;

            if (!Offered.Any) return false;

            var words = new string[Offered.Answers.Count];
            var open = new bool[Offered.Answers.Count];

            for (int at = 0; at < Offered.Answers.Count; at++)
            {
                words[at] = _text.Get(Offered.Answers[at].Key);
                open[at] = Offered.Answers[at].Open;
            }

            Words = words;

            if (Offered.As == Laid.Cards) { Deal(words, open); return true; }

            if (_dm != null && _dm.Asks(words, open)) return true;

            // no DM to push the note. Dealing them as cards is wrong past a handful and it is
            // still the only thing at this table that is not a list, so it is what happens - and
            // it says so, because a table with nobody across it is a scene wired up wrong
            GD.PushWarning("response: there is nobody across the table to push a note, so " +
                           $"{words.Length} written option(s) are lying out as cards instead");

            Deal(words, open);

            return true;
        }

        void Deal(IReadOnlyList<string> words, IReadOnlyList<bool> open)
        {
            for (int at = 0; at < words.Count; at++)
            {
                var card = new ChoiceCard
                {
                    Name = "Answer" + at,

                    // a shade of height per card so a row of them never z-fights on the table
                    Position = CardsAt + new Vector3(0f, 0.0004f * at, CardGap * at),
                };

                AddChild(card);

                card.Deal(at, words[at], open[at]);

                _cards.Add(card);

                GD.Print($"answer  [{at}] {words[at]}" + (open[at] ? "" : " (closed)"));
            }
        }

        // everything the last offer put on the table, taken off
        public void Sweep()
        {
            foreach (ChoiceCard card in _cards) card.QueueFree();

            _cards.Clear();

            _dm?.TakesItBack();

            Offered = Offer.Nothing;
            Words = new string[0];
            _lit = -1;
        }

        // TAKEN, from outside: what a keyboard walk and a headless check use, and what the click
        // below ends up calling. One way in, so a mouse and a key cannot answer differently.
        public bool Take(int index)
        {
            Answer answer = Offered.At(index);

            if (answer == null || !answer.Open) return false;

            GD.Print($"answer  {answer}");

            // the tick is marked before the paper goes back, so you see the DM read your answer
            _dm?.Asking?.Tick(index);

            Taken = answer;

            Sweep();

            EmitSignal(SignalName.Answered, index);

            return true;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Asking) return;

            if (@event is InputEventMouseMotion motion) { Hover(motion.Position); return; }

            if (!@event.IsActionPressed("place_piece") || @event is not InputEventMouse mouse) return;

            int at = Under(mouse.Position);

            if (at < 0) return;

            GetViewport().SetInputAsHandled();

            Take(at);
        }

        void Hover(Vector2 at)
        {
            int over = Under(at);

            if (over == _lit) return;

            if (_lit >= 0 && _lit < _cards.Count) _cards[_lit].Light(false);

            _lit = over;

            if (_lit >= 0 && _lit < _cards.Count) _cards[_lit].Light(true);

            _dm?.Asking?.Light(_cards.Count == 0 ? over : -1);
        }

        // which answer is under the cursor, cards and note rows alike; -1 for neither
        int Under(Vector2 at)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return -1;

            var query = PhysicsRayQueryParameters3D.Create(
                camera.ProjectRayOrigin(at),
                camera.ProjectRayOrigin(at) + camera.ProjectRayNormal(at) * Reach);

            query.CollideWithAreas = false;

            Godot.Collections.Dictionary hit = GetWorld3D()?.DirectSpaceState?.IntersectRay(query);

            if (hit == null || hit.Count == 0) return -1;

            var what = hit["collider"].As<GodotObject>();

            for (int card = 0; card < _cards.Count; card++)
                if (_cards[card].Owns(what)) return _cards[card].Offered ? card : -1;

            int row = _dm?.Asking?.RowUnder(what) ?? -1;

            return row >= 0 && _dm.Asking.IsOpen(row) ? row : -1;
        }

        const float Reach = 8f;

        // developer only, not localized, never reaches the screen
        public override string ToString() => Asking ? $"response: {Offered}" : "response: nothing asked";
    }
}

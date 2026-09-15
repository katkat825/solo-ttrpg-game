using System;
using System.Collections.Generic;
using Godot;
using Content.Dialogue;
using Core.Localization;
using Game.Companion;
using Game.Localization;

namespace Game.Dialogue
{
    // A CONVERSATION, RUNNING AT THE TABLE (W0).
    //
    // The branching is Yarn Spinner's and the words are the locale's; this node owns neither. All
    // it does is put a line in the right creature's bubble, lay the choices out as cards, and pass
    // the click back. That division is the whole reason ARCHITECTURE.md section 6 said not to write
    // a dialogue runtime: the part that was ever going to be ours is this hundred lines of table.
    //
    // Who says a line is decided by the line's OWN speaker, not by whose scene it is, so a
    // conversation between the companion and an NPC lands in the right two places.
    [GlobalClass]
    public partial class Talk : Node3D
    {
        // the companion at this table; lines keyed to its creature come out of its mouth
        [Export] public NodePath CompanionPath { get; set; }

        // the DM; lines whose speaker is the DM are pushed across the table instead (D5)
        [Export] public NodePath DmPath { get; set; }

        // where a line with no home goes - an NPC's, mostly. Made if it is not there
        [Export] public NodePath BubblePath { get; set; } = "Bubble";

        [Export] public Vector3 ChoicesAt { get; set; } = new Vector3(0f, 0.002f, 0.26f);

        [Export] public float ChoiceGap { get; set; } = 0.062f;

        // a line advances on its own once it has been up long enough; a click takes it sooner
        [Export] public bool Automatic { get; set; } = true;

        public const string DmSpeaker = "dm";

        [Signal] public delegate void FinishedEventHandler();

        [Signal] public delegate void SaidEventHandler(string key);

        Game.Companion.Companion _companion;

        Game.Dm.Dm _dm;

        Bubble _elsewhere;

        readonly List<ChoiceCard> _cards = new List<ChoiceCard>();

        readonly ILocalizer _text = new GodotLocalizer();

        Conversation _talk;

        double _left;

        public bool Running => _talk != null && !_talk.IsOver;

        public bool Choosing => _talk is { IsChoosing: true };

        public Conversation Conversation => _talk;

        // every key said this conversation, for a headless check to read back
        public IReadOnlyList<string> Heard { get; private set; } = Array.Empty<string>();

        public override void _Ready()
        {
            if (CompanionPath != null && !CompanionPath.IsEmpty)
                _companion = GetNodeOrNull<Game.Companion.Companion>(CompanionPath);

            if (DmPath != null && !DmPath.IsEmpty) _dm = GetNodeOrNull<Game.Dm.Dm>(DmPath);

            _elsewhere = GetNodeOrNull<Bubble>(BubblePath);

            if (_elsewhere == null)
            {
                _elsewhere = new Bubble { Name = "Bubble", Position = new Vector3(0f, 0.10f, -0.10f) };
                AddChild(_elsewhere);
            }
        }

        // false when this campaign has no such conversation, which is a content problem already
        // named at load and not a reason to stop the game
        public bool Begin(DialogueBook book, string node)
        {
            _Ready();

            if (book?.Program == null || !book.Has(node))
            {
                GD.PushError($"talk: there is no conversation called '{node}' in this campaign");
                return false;
            }

            _talk = new Conversation(book);

            if (!_talk.Start(node)) { Over(); return false; }

            GD.Print("");
            GD.Print($"talk    {node}, {book.SpeakerOf(node)} speaking");

            Deliver();

            return true;
        }

        public void Stop()
        {
            Clear();
            _talk = null;
            _left = 0.0;
        }

        // the line is up; put it where it belongs and set the clock on it
        void Deliver()
        {
            if (_talk == null) return;

            if (_talk.IsOver) { Over(); return; }

            if (_talk.IsChoosing) { Lay(); return; }

            Conversation.Said saying = _talk.Saying;

            // a line the loader refused has no key and is stepped over rather than shown blank
            if (saying == null) { Step(); return; }

            string words = saying.Text(_text);

            _left = Reading.Time(words);

            EmitSignal(SignalName.Said, saying.Key);

            if (_dm != null && saying.Speaker == DmSpeaker)
            {
                _dm.Says(saying.Key);
                return;
            }

            if (_companion != null && saying.Speaker == _companion.Speaker)
            {
                _companion.Line(saying.Key);
                return;
            }

            _elsewhere.Say(words, _left);

            GD.Print($"        {saying.Speaker}: \"{words}\"");
        }

        void Step()
        {
            _talk.Advance();

            Deliver();
        }

        void Lay()
        {
            Clear();

            _left = 0.0;

            int at = 0;

            foreach (Conversation.Choice choice in _talk.Choosing)
            {
                var card = new ChoiceCard
                {
                    Name = "Choice" + at,
                    Position = ChoicesAt + new Vector3(0f, 0.0004f * at, ChoiceGap * at),
                };

                AddChild(card);

                card.Deal(choice.Index, choice.Text(_text), choice.Offered);

                _cards.Add(card);

                GD.Print($"        -> {card.Said}" + (choice.Offered ? "" : " (closed)"));

                at++;
            }
        }

        void Clear()
        {
            foreach (ChoiceCard card in _cards) card.QueueFree();

            _cards.Clear();
        }

        void Over()
        {
            Clear();

            Heard = _talk == null
                ? Array.Empty<string>()
                : System.Linq.Enumerable.ToArray(
                      System.Linq.Enumerable.Select(_talk.Heard, s => s.Key));

            foreach (string complaint in _talk?.Complained ?? new List<string>())
                GD.PushWarning("talk: " + complaint);

            _talk = null;
            _left = 0.0;

            GD.Print($"talk    over, {Heard.Count} lines");

            EmitSignal(SignalName.Finished);
        }

        // a click on a card takes it; a click anywhere else takes the line
        public override void _UnhandledInput(InputEvent @event)
        {
            if (_talk == null) return;

            if (!@event.IsActionPressed("place_piece") || @event is not InputEventMouse mouse) return;

            if (_talk.IsChoosing)
            {
                ChoiceCard card = Under(mouse.Position);

                if (card == null || !card.Offered) return;

                GetViewport().SetInputAsHandled();

                Take(card);
                return;
            }

            GetViewport().SetInputAsHandled();

            Step();
        }

        void Take(ChoiceCard card)
        {
            GD.Print($"        \"{card.Said}\"");

            Clear();

            _talk.Choose(_talk.Choosing.Count > 0 ? IndexOf(card.Option) : 0);

            Deliver();
        }

        int IndexOf(int option)
        {
            for (int at = 0; at < _talk.Choosing.Count; at++)
                if (_talk.Choosing[at].Index == option) return at;

            return 0;
        }

        ChoiceCard Under(Vector2 at)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return null;

            var query = PhysicsRayQueryParameters3D.Create(
                camera.ProjectRayOrigin(at),
                camera.ProjectRayOrigin(at) + camera.ProjectRayNormal(at) * Reach);

            query.CollideWithAreas = false;

            Godot.Collections.Dictionary hit = GetWorld3D()?.DirectSpaceState?.IntersectRay(query);

            if (hit == null || hit.Count == 0) return null;

            var what = hit["collider"].As<GodotObject>();

            foreach (ChoiceCard card in _cards)
                if (card.Owns(what)) return card;

            return null;
        }

        const float Reach = 8f;

        public override void _Process(double delta)
        {
            if (_talk == null || _talk.IsChoosing || !Automatic) return;

            if (_left <= 0.0) return;

            _left -= delta;

            if (_left <= 0.0) Step();
        }

        // Drive the whole conversation with no table - what a headless check walks a campaign's
        // dialogue with.
        //
        // 'prefer' is which open choice to take at every fork, clamped to what is on offer. One
        // walk reaches one path through a branching scene, so a check that wants the whole thing
        // walks it once per prefer and unions the result; that covers every branch of every fork
        // without the bookkeeping of real backtracking, which for a conversation is not worth it.
        public static IReadOnlyList<string> Walk(DialogueBook book, string node, int prefer = 0,
                                                 int most = 400)
        {
            var heard = new List<string>();

            if (book?.Program == null || !book.Has(node)) return heard;

            var talk = new Conversation(book);

            if (!talk.Start(node)) return heard;

            int guard = 0;

            while (!talk.IsOver && guard++ < most)
            {
                if (talk.Saying != null) heard.Add(talk.Saying.Key);

                if (!talk.IsChoosing) { talk.Advance(); continue; }

                var open = new List<int>();

                for (int at = 0; at < talk.Choosing.Count; at++)
                {
                    // a closed option's line is added anyway: it is shown greyed, so a player
                    // reads it, so it needs words like any other line
                    heard.Add(talk.Choosing[at].Key);

                    if (talk.Choosing[at].Offered) open.Add(at);
                }

                if (open.Count == 0) break;

                if (!talk.Choose(open[System.Math.Min(prefer, open.Count - 1)])) break;
            }

            return heard;
        }
    }
}

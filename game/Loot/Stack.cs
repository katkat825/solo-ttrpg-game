using System.Collections.Generic;
using Godot;
using Content.Campaigns;
using Core.Characters;
using Core.Localization;
using Game.Localization;

namespace Game.Loot
{
    // THE STACK OF CARDS BESIDE YOU - WHICH IS THE WHOLE INVENTORY.
    //
    // Anything you gain, the DM hands across as a card and it goes on this stack. Turning one over
    // reads it. There is no bag, no grid of slots and no weight, because none of those is a thing
    // on a table and the stack is.
    //
    // Dealt is the stack and this is the objects: the cards lie where Dealt says, one turns over
    // at a time because Dealt says so, and this does the reaching and the drawing.
    [GlobalClass]
    public partial class Stack : Node3D
    {
        // whose hands deal a card across. Without one the card simply appears, which is a table
        // with nobody sitting at it rather than a bug
        [Export] public NodePath DmPath { get; set; }

        [Signal] public delegate void TookEventHandler(string item);

        readonly Dealt _dealt = new Dealt();

        readonly List<ItemCard> _cards = new List<ItemCard>();

        readonly ILocalizer _text = new GodotLocalizer();

        Game.Dm.Dm _dm;

        Game.Campaigns.Library _shelf;

        int _lit = -1;

        public Dealt Carrying => _dealt;

        public int Count => _dealt.Count;

        public string Reading => _dealt.Reading;

        public override void _Ready()
        {
            if (DmPath != null && !DmPath.IsEmpty) _dm = GetNodeOrNull<Game.Dm.Dm>(DmPath);

            _shelf = Game.Campaigns.Library.Load(quiet: true);
        }

        // one card, dealt across. The id may be a campaign's own or the base game's; whichever it
        // is, the words on it come off the item and go through the localizer like everything else
        public bool Deal(string campaign, string item)
        {
            if (string.IsNullOrWhiteSpace(item)) return false;

            _Ready();

            string scoped = ContentId.IsCampaign(campaign) && !ContentId.IsScoped(item)
                ? ContentId.Scoped(campaign, item)
                : item;

            Gear gear = _shelf?.Items?.Of(scoped) ?? _shelf?.Items?.Of(item);

            string named = gear != null ? _text.Get(gear.NameKey) : scoped;
            string detail = gear != null && _text.Has(gear.DescriptionKey)
                ? _text.Get(gear.DescriptionKey)
                : "";

            if (gear == null)
                GD.PushWarning($"loot: nothing on the shelf is called '{scoped}', so the card " +
                               "carries its id rather than its name");

            int at = _dealt.Deal(scoped);

            if (at < 0) return false;

            var card = new ItemCard { Name = "Card" + at, Position = _dealt.Sits(at) };

            AddChild(card);

            card.Deal(scoped, named, detail);

            _cards.Add(card);

            _dm?.Does(Content.Places.Gesture.Push);

            GD.Print($"loot    a card comes across - \"{named}\"");

            EmitSignal(SignalName.Took, scoped);

            return true;
        }

        // turn one over to read it; turning the one that is up puts it back down
        public bool Turn(int index)
        {
            if (!_dealt.Flip(index)) return false;

            Relay();

            GD.Print(_dealt.FaceUp >= 0
                ? $"loot    turned over \"{_cards[_dealt.FaceUp].Named}\""
                : "loot    the stack is square again");

            return true;
        }

        void Relay()
        {
            for (int at = 0; at < _cards.Count; at++)
            {
                _cards[at].Position = _dealt.Sits(at);
                _cards[at].Turn(at == _dealt.FaceUp);
            }
        }

        public void Sweep()
        {
            foreach (ItemCard card in _cards) card.QueueFree();

            _cards.Clear();
            _dealt.Sweep();
            _lit = -1;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_cards.Count == 0) return;

            if (@event is InputEventMouseMotion motion) { Hover(motion.Position); return; }

            if (!@event.IsActionPressed("place_piece") || @event is not InputEventMouse mouse) return;

            int at = Under(mouse.Position);

            if (at < 0) return;

            GetViewport().SetInputAsHandled();

            Turn(at);
        }

        void Hover(Vector2 at)
        {
            int over = Under(at);

            if (over == _lit) return;

            if (_lit >= 0 && _lit < _cards.Count) _cards[_lit].Light(false);

            _lit = over;

            if (_lit >= 0 && _lit < _cards.Count) _cards[_lit].Light(true);
        }

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

            // last first: the card on top of the stack is the one the cursor is actually over
            for (int card = _cards.Count - 1; card >= 0; card--)
                if (_cards[card].Owns(what)) return card;

            return -1;
        }

        const float Reach = 8f;

        // developer only, not localized, never reaches the screen
        public override string ToString() => "stack: " + _dealt;
    }
}

using System;
using Godot;
using Core.Localization;
using Game.Localization;

namespace Game.Book
{
    // A BOOK YOU PICK UP AND OPEN (BK1).
    //
    // The one object that knows what a book looks like. R4 stood a campaign up as a coloured box and
    // then, on the way to this, as a spine drawn inline inside Room.Stand - which meant the room and
    // the bookcase would each have had their own idea of what a book was, and a book drawn twice is
    // a book that drifts. Both stand these now.
    //
    // It has no Open() and no page of its own, for the same reason Furniture has no menu: the object
    // is the thing, and what picking it up MEANS is decided where the objects are, in one place you
    // can read. This reports that it was touched and says which book it is.
    [GlobalClass]
    public partial class Book : Node3D
    {
        // the campaign this is a copy of. Empty for the rules book, which belongs to no campaign
        [Export] public string Campaign { get; set; } = "";

        [Export] public Tome Is { get; set; } = Tome.Campaign;

        // a trophy on the book, not beside it - it belongs to that campaign (R4)
        [Export] public bool Finished { get; set; }

        // a blank campaign book on the bookcase is the Workshop door for a campaign (BK6). It is
        // still a book and still stands in a row of them, because that is the point of it
        [Export] public bool Unwritten { get; set; }

        [Export] public float Thickness { get; set; } = 0.035f;

        [Export] public float Tall { get; set; } = 0.22f;

        [Export] public float Deep { get; set; } = 0.17f;

        // how it lights up under the cursor. The only affordance a book has, because a tooltip
        // would be a panel
        [Export] public float Lift { get; set; } = 0.22f;

        [Signal] public delegate void TouchedEventHandler(string campaign, int tome);

        MeshInstance3D _covers;

        StandardMaterial3D _binding;

        StaticBody3D _touch;

        Color _plain;

        readonly ILocalizer _text = new GodotLocalizer();

        // what a player would call it: the campaign's own name, or the rules book's. Read once, the
        // same way Furniture reads its own - a Label3D per book is a tooltip in disguise
        public string Called { get; private set; } = "";

        public override void _Ready()
        {
            if (_covers != null) return;

            _plain = Unwritten ? Unpainted : Colour(Is == Tome.Rules ? RulesSpine : Campaign);

            _binding = new StandardMaterial3D { AlbedoColor = _plain, Roughness = 0.95f };

            _covers = new MeshInstance3D
            {
                Name = "Covers",
                Mesh = new BoxMesh { Size = new Vector3(Thickness, Tall, Deep) },
                MaterialOverride = _binding,
            };

            AddChild(_covers);

            // THE PAGES. One paler slab, a hair narrower and shorter than the covers and sitting
            // proud of the fore edge - which is the whole difference between a book seen from the
            // side and a painted block, and what a row of boxes was missing.
            _covers.AddChild(new MeshInstance3D
            {
                Name = "Pages",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(Thickness * 0.72f, Tall * 0.94f, Deep * 0.96f),
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color("#d8cdb4"),
                    Roughness = 1.0f,
                },
                Position = new Vector3(0f, 0f, Deep * 0.035f),
            });

            if (Finished)
                _covers.AddChild(new MeshInstance3D
                {
                    Name = "Trophy",
                    Mesh = new SphereMesh { Radius = 0.016f, Height = 0.032f },
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color("#c9a227"),
                        Metallic = 0.8f,
                        Roughness = 0.35f,
                    },
                    Position = new Vector3(0f, Tall * 0.5f + 0.02f, 0f),
                });

            _touch = new StaticBody3D { Name = "Touch" };

            // generously wider than the spine: a 35mm book is a hard thing to hit with a cursor, and
            // a hitbox you have to aim at is an accessibility failure
            Span = Game.Access.Hitbox.Around(new Vector3(Thickness * 2.2f, Tall, Deep));

            _touch.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Span } });

            AddChild(_touch);

            Called = Titled();
        }

        // WHAT IT IS CALLED, WHICH IS NOT THE SAME AS WHAT IS PRINTED ON IT. A blank ribbon has
        // nothing written on its spine, deliberately - and it is still a thing you can pick up, so it
        // still needs a name, or it is a stop a screen reader arrives at in silence. The access check
        // found exactly that: three nameless things on the case.
        string Titled()
        {
            if (Unwritten) return _text.Get(BookKeys.NewCharacter);

            if (Is == Tome.Rules) return _text.Get(Is.NameKey());

            return Campaign.Length == 0
                ? _text.Get(Tome.Campaign.NameKey())
                : _text.Get(KeyConventions.Key(KeyConventions.CampaignNs, Campaign, "name"));
        }

        public Vector3 Span { get; private set; }

        public bool Owns(GodotObject what) => _touch != null && ReferenceEquals(_touch, what);

        public Game.Access.Reachable Reach() =>
            new Game.Access.Reachable(Called, Touch, _touch, Span, lit: Light);

        public void Light(bool lit)
        {
            if (_binding == null) return;

            _binding.AlbedoColor = lit ? _plain.Lightened(Lift) : _plain;
        }

        public void Touch() => EmitSignal(SignalName.Touched, Campaign, (int)Is);

        // the rules book's spine gets a colour of its own rather than a campaign's, so it reads as
        // the odd one out in a row - which is what it is
        public const string RulesSpine = "the_rules";

        // an unwritten book is bare board: the blank objects are meant to look unfinished
        public static readonly Color Unpainted = new Color("#b9ae97");

        // each campaign's volumes look alike, so a row of them reads as one campaign's books rather
        // than as a colour chart
        public static Color Colour(string campaign)
        {
            if (string.IsNullOrEmpty(campaign)) return Unpainted;

            int hash = 17;

            foreach (char c in campaign) hash = hash * 31 + c;

            var rng = new RandomNumberGenerator { Seed = (ulong)Math.Abs(hash) };

            return Color.FromHsv(rng.Randf(), 0.35f, rng.RandfRange(0.35f, 0.6f));
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{(Unwritten ? "a blank" : Is.Word())} {(Campaign.Length > 0 ? Campaign : "")}" +
            (Finished ? ", finished" : "");
    }
}

using Godot;
using Content.Campaigns;
using Content.Dialogue;

namespace Game.Companion
{
    // THE HINT SYSTEM (W4, CORE_RULES.md section 12): voice only, zero mechanics, and PULLED.
    //
    // The player asks; the companion never volunteers. So the surface is a thing you reach for and
    // not a prompt that appears - the table's own affordance for "I am going to bother you", which
    // is what asking a real person at a real table feels like.
    //
    // Three rungs on repeat asks, then the companion notices it is being over-asked and says so in
    // character. It gates nothing: every rung is a line, and the information behind it is always
    // reachable another way. That is a rule about how campaigns are authored, and the validator
    // cannot check it - which is why it is written here, where somebody adding a fourth tier will
    // read it.
    [GlobalClass]
    public partial class HintCord : Node3D
    {
        [Export] public NodePath CompanionPath { get; set; }

        [Export] public Key AskWith { get; set; } = Key.H;

        [Export] public Color Braid { get; set; } = new Color("#8a7654");

        readonly HintLadder _ladder = new HintLadder();

        Game.Companion.Companion _companion;

        // where a beat falls when the companion at this table has no phrasing of it
        [Export] public NodePath DmPath { get; set; }

        Game.Dm.Dm _dm;

        readonly Core.Localization.ILocalizer _text = new Game.Localization.GodotLocalizer();

        Package _campaign;

        // the problem the player is in front of; the room sets it, and it is what the ladder counts
        public string About { get; set; } = "";

        public HintLadder Ladder => _ladder;

        public int AsksSoFar => _ladder.AsksAbout(About);

        public override void _Ready()
        {
            if (CompanionPath != null && !CompanionPath.IsEmpty)
                _companion = GetNodeOrNull<Game.Companion.Companion>(CompanionPath);

            if (DmPath != null && !DmPath.IsEmpty) _dm = GetNodeOrNull<Game.Dm.Dm>(DmPath);

            Build();
        }

        // A CORD YOU CAN SEE IS A CORD. Static, cheap, and it has to read as a thing to pull.
        //
        // It did not. A bare 9 cm cylinder lying at an angle on a table is a twig, and the eye
        // check of 2026-09-16 called it exactly that: "a random line or stick below the bottom
        // left corner of the map". The braid was right and everything holding it up was missing.
        //
        // Three parts and it reads: a ring screwed into the table, the braid hanging from it, and
        // a wooden knob on the end at the height a hand closes round. The knob is what does most
        // of the work - a cord with a handle on it is asking to be pulled, and a cord without one
        // is string.
        void Build()
        {
            if (GetNodeOrNull("Cord") != null) return;

            var braid = new StandardMaterial3D { AlbedoColor = Braid, Roughness = 1f };

            var brass = new StandardMaterial3D
            {
                AlbedoColor = Braid.Darkened(0.35f),
                Roughness = 0.55f,
                Metallic = 0.6f,
            };

            var cord = new MeshInstance3D
            {
                Name = "Cord",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.0022f,
                    BottomRadius = 0.0022f,
                    Height = 0.09f,
                    RadialSegments = 6,
                },
                MaterialOverride = braid,
            };

            AddChild(cord);

            // the anchor: without it the braid hangs from nothing and the eye reads it as debris
            var ring = new MeshInstance3D
            {
                Name = "Ring",
                Mesh = new TorusMesh
                {
                    InnerRadius = 0.004f,
                    OuterRadius = 0.007f,
                    RingSegments = 8,
                    Rings = 6,
                },
                Position = new Vector3(0f, 0.046f, 0f),
                MaterialOverride = brass,
            };

            ring.RotateX(Mathf.DegToRad(90f));

            AddChild(ring);

            // and the handle, at the end a hand reaches for
            AddChild(new MeshInstance3D
            {
                Name = "Knob",
                Mesh = new SphereMesh
                {
                    Radius = 0.0075f,
                    Height = 0.017f,
                    RadialSegments = 10,
                    Rings = 6,
                },
                Position = new Vector3(0f, -0.050f, 0f),
                MaterialOverride = brass,
            });
        }

        public void Knows(Package campaign) => _campaign = campaign;

        // the problem changed: a new room is a new question, and the ladder starts at the bottom
        public void NowAbout(string problem) => About = problem ?? "";

        public void Solved()
        {
            _ladder.Solved(About);

            About = "";
        }

        // the key said, or null when there is nothing to ask about here
        public string Pull()
        {
            if (_companion == null) return null;

            if (About.Length == 0)
            {
                GD.Print("hint    nothing in this room is a question yet");
                return null;
            }

            Hint hint = _campaign?.Hints.Of(About);

            if (hint == null)
            {
                GD.Print($"hint    this campaign has written no ladder for '{About}'");
                return null;
            }

            Ask ask = _ladder.Next(About);

            if (ask.Overasked)
            {
                GD.Print($"hint    asked about '{About}' {_ladder.AsksAbout(About)} times now");

                // the self-regulation, and it stays in the fiction: the fiend charges more, the
                // hound gets anxious, the saint gets disappointed
                return _companion.Say(Bark.Overasked);
            }

            string beat = hint.Beat(ask.Rung);

            if (beat == null) return null;

            GD.Print($"hint    '{About}', rung {ask.Rung} of {HintLadder.Rungs}");

            string mine = DialogueKeys.Beat(_companion.Speaker, _campaign.Id, beat);

            // the companion's own phrasing where the campaign wrote one; the DM's otherwise, so a
            // hint is never silence just because you brought a companion the author had not met
            if (_text.Has(mine)) return _companion.Line(mine);

            string dm = DialogueKeys.Beat(Beat.TheDm, _campaign.Id, beat);

            if (!_text.Has(dm))
            {
                GD.Print($"hint    nobody at this table has a phrasing of '{beat}'");
                return null;
            }

            _dm?.Says(dm);

            return dm;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

            if (key.Keycode != AskWith) return;

            GetViewport().SetInputAsHandled();

            Pull();
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            About.Length == 0 ? "cord: no question here"
                              : $"cord: '{About}', asked {AsksSoFar} times";
    }
}

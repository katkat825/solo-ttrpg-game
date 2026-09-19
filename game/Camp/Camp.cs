using Godot;
using Content.Dialogue;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Game.Companion;

namespace Game.Camp
{
    // CAMP (W3, CORE_RULES.md section 11).
    //
    // "Build camp as a scene at the table - the fire, the companion, the conversation - not a rest
    // button with a menu behind it." So this is a Node3D that puts a fire on the table, moves the
    // companion to it and starts a conversation; the rest effects are a side effect of the night
    // rather than the substance of it.
    //
    // The rules half is Rest.Camp in core/ and knows nothing about any of this. The topic comes
    // from the Day, which watched the fights; nothing here asks the player what the night is about.
    [GlobalClass]
    public partial class Camp : Node3D
    {
        [Export] public NodePath CompanionPath { get; set; }

        [Export] public NodePath TalkPath { get; set; } = "Talk";

        // where the fire stands on the table, and where the companion comes to sit by it
        [Export] public Vector3 FireAt { get; set; } = new Vector3(0f, 0.004f, 0.12f);

        [Export] public Color Firelight { get; set; } = new Color("#ff9a4a");

        [Export] public float FireEnergy { get; set; } = 1.6f;

        [Export] public int Seed { get; set; }

        [Signal] public delegate void RestedEventHandler();

        Game.Companion.Companion _companion;

        Game.Dialogue.Talk _talk;

        OmniLight3D _fire;

        Node3D _logs;

        Campfire _fireside;

        Vector3 _wasAt;

        // one at a time, killed rather than left to finish - the same reason the companion holds its own
        Tween _walking;

        double _flicker;

        public bool Lit { get; private set; }

        // what the last night did, so the table can show it; null before the first camp
        public Camped? LastNight { get; private set; }

        public string Tonight { get; private set; } = "";

        public override void _Ready()
        {
            if (CompanionPath != null && !CompanionPath.IsEmpty)
                _companion = GetNodeOrNull<Game.Companion.Companion>(CompanionPath);

            _talk = GetNodeOrNull<Game.Dialogue.Talk>(TalkPath);

            if (_talk == null)
            {
                // THE PATH IS THE CHILD'S, NOT THIS NODE'S. CompanionPath is written relative to
                // the camp ("../Companion"), and read from a child one level down that is
                // "Camp/Companion", which is nothing - so the companion's own lines at the fire
                // came out of the table's bubble instead of its mouth, silently, from the day this
                // was built. Hand the child the node that was already found, by its own full path.
                _talk = new Game.Dialogue.Talk
                {
                    Name = "Talk",
                    CompanionPath = _companion != null ? _companion.GetPath() : default,
                };

                AddChild(_talk);
            }

            Build();
            Out();
        }

        // a low fire and three logs; nothing here is expensive (THE_TABLE.md section 6)
        void Build()
        {
            if (_logs != null) return;

            _logs = new Node3D { Name = "Logs", Position = FireAt };

            var char_ = new StandardMaterial3D
            {
                AlbedoColor = new Color("#2b2119"),
                Roughness = 1f,
            };

            for (int at = 0; at < 3; at++)
            {
                var log = new MeshInstance3D
                {
                    Name = "Log" + at,
                    Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.008f, 0.010f) },
                    MaterialOverride = char_,
                    Position = new Vector3(0f, 0.004f, 0f),
                    Rotation = new Vector3(0f, Mathf.Pi / 3f * at, 0f),
                };

                _logs.AddChild(log);
            }

            _fire = new OmniLight3D
            {
                Name = "Fire",
                LightColor = Firelight,
                LightEnergy = FireEnergy,
                OmniRange = 0.9f,
                Position = new Vector3(0f, 0.03f, 0f),
                ShadowEnabled = true,
            };

            _logs.AddChild(_fire);

            AddChild(_logs);
        }

        // the night. Rest first, so a companion talking about your wounds is talking about the ones
        // you woke up with rather than the ones you went to sleep with.
        public bool Make(Actor hero, Day day, Content.Campaigns.Package campaign)
        {
            if (Lit) return false;

            Camped night = Rest.Camp(hero);

            LastNight = night;

            GD.Print("");
            GD.Print($"camp    {night}");

            In();

            Tonight = Scene(day, campaign);

            if (Tonight.Length == 0)
            {
                GD.Print("camp    this campaign has written no scene for tonight, so the fire is quiet");
            }
            else if (!_talk.Begin(campaign.Dialogue, Tonight))
            {
                Tonight = "";
            }

            day?.Slept();

            EmitSignal(SignalName.Rested);

            return true;
        }

        string Scene(Day day, Content.Campaigns.Package campaign)
        {
            if (campaign?.Dialogue?.Program == null || day == null) return "";

            _fireside ??= new Campfire(campaign.Dialogue,
                                       new SeededRng(Seed != 0 ? Seed : (int)Time.GetTicksMsec()));

            GD.Print($"camp    the day was {day} - the fire knows {_fireside}");

            return _fireside.Tonight(day) ?? "";
        }

        public void Douse()
        {
            if (!Lit) return;

            _talk?.Stop();

            Out();
        }

        void In()
        {
            Lit = true;

            _logs.Visible = true;
            _fire.Visible = true;

            if (_companion == null) return;

            // it comes and sits by the fire, and goes back to its own place afterwards
            _wasAt = _companion.Position;

            _walking?.Kill();
            _walking = CreateTween();
            _walking.SetTrans(Tween.TransitionType.Sine);
            _walking.SetEase(Tween.EaseType.InOut);
            _walking.TweenProperty(_companion, "position",
                               ToGlobal(FireAt + new Vector3(0.08f, 0f, 0.03f)) - _companion.GetParent<Node3D>().GlobalPosition,
                               1.1f);

            _companion.Settle();
            _companion.Say(Bark.Camp);
        }

        void Out()
        {
            Lit = false;

            if (_logs != null) _logs.Visible = false;
            if (_fire != null) _fire.Visible = false;

            if (_companion == null || _wasAt == Vector3.Zero) return;

            _walking?.Kill();
            _walking = CreateTween();
            _walking.SetTrans(Tween.TransitionType.Sine);
            _walking.TweenProperty(_companion, "position", _wasAt, 0.9f);
        }

        public override void _ExitTree() => _walking?.Kill();

        // a fire that does not move is a light, not a fire
        public override void _Process(double delta)
        {
            if (!Lit || _fire == null) return;

            _flicker += delta;

            _fire.LightEnergy = FireEnergy *
                (0.88f + 0.12f * Mathf.Sin((float)_flicker * 7.3f) * Mathf.Cos((float)_flicker * 3.1f));
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            Lit ? $"camp lit, {(Tonight.Length > 0 ? Tonight : "nothing said")}" : "camp out";
    }
}

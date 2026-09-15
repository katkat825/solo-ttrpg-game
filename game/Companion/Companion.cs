using System.Collections.Generic;
using Godot;
using Content.Companions;
using Content.Dialogue;
using Core.Dice;
using Core.Localization;
using Game.Localization;

namespace Game.Companion
{
    // THE COMPANION, ALIVE ON THE TABLE (W1, THE_TABLE.md section 4).
    //
    // The conceit: every mini on the table is inert; yours is alive. So it sits on the TABLE and
    // never on the map - which is a promise about the fiction and, more usefully, a structural
    // guarantee. There is no Cell here, no Actor, no place to stand, and so no later milestone can
    // absent-mindedly give it a turn (CORE_RULES.md section 13).
    //
    // It reacts to the TABLE, not the fiction: it watches the dice tumble, turns to the screen on a
    // secret roll, goes quiet when you tip over. Everything it knows, it knows by being told by
    // something that was already watching - it polls nothing and owns no rules.
    //
    // Placeholder geometry on purpose. W1: build the idles first, real behaviour, and let the model
    // arrive later - a dozen idles read as a creature and a beautiful statue does not.
    [GlobalClass]
    public partial class Companion : Node3D
    {
        // which card to be, off the shelf; empty leaves a nameless placeholder that still idles
        [Export] public string CompanionId { get; set; } = "";

        // set in the scene where a companion is placed by hand rather than picked by a class
        [Export] public string Voice { get; set; } = "";

        [Export] public NodePath BubblePath { get; set; } = "Bubble";

        // what it turns toward on a secret roll, and what it looks at when you are about to do
        // something unwise. Either may be unset; it simply does not turn.
        [Export] public NodePath ScreenPath { get; set; }

        [Export] public NodePath TrayPath { get; set; }

        [Export] public int Seed { get; set; }

        [Export] public Color Coat { get; set; } = new Color("#6a6258");

        // how far the head swings when it looks at something, in radians
        [Export] public float Turn { get; set; } = 0.55f;

        [Export] public float TurnSeconds { get; set; } = 0.35f;

        Bubble _bubble;

        Node3D _head;

        Node3D _body;

        Node3D _screen;

        Node3D _tray;

        Idling _idling;

        Speaking _speaking;

        IRng _rng;

        readonly ILocalizer _text = new GodotLocalizer();

        Mood _mood = Mood.Calm;

        double _holding;

        Tween _turning;

        // held and killed rather than left to finish. A Tween lives until it completes, and this
        // node starts one every time an idle changes - which headless, with no frame rate to pace
        // it, is faster than they retire. Holding one means there is only ever one.
        Tween _shifting;

        // what it has said, for a headless check and for the eye check's console; developer
        // diagnostics, never a second copy of the script
        readonly List<string> _spoken = new List<string>();

        public IReadOnlyList<string> Spoken => _spoken;

        // LOOKED UP LAZILY, and that is not tidiness. The tray and the fight both ask a companion
        // who it is from inside their own _Ready, and Godot readies siblings in tree order - so a
        // companion that only knew itself after _Ready would answer "nobody" to whichever of them
        // sits above it in the scene, and the tray would spend the whole session on M9's
        // placeholder bark keys without ever saying so.
        CompanionCard _card;

        bool _looked;

        public CompanionCard Card
        {
            get
            {
                if (_looked) return _card;

                _looked = true;

                return _card = Look();
            }
        }

        public Mood Feeling => _mood;

        public int Idle => _idling?.Idle ?? 0;

        // the creature it speaks as - what its bark keys are filed under
        public string Speaker =>
            Voice.Length > 0 ? Voice
          : Card != null ? Card.Voice
          : "";

        public bool CanSpeak => _speaking != null;

        public override void _Ready()
        {
            _rng = new SeededRng(Seed != 0 ? Seed : (int)Time.GetTicksMsec());

            Shape();

            _bubble = GetNodeOrNull<Bubble>(BubblePath);

            if (_bubble == null)
            {
                _bubble = new Bubble { Name = "Bubble", Position = new Vector3(0f, 0.075f, 0.02f) };
                AddChild(_bubble);
            }

            if (ScreenPath != null && !ScreenPath.IsEmpty) _screen = GetNodeOrNull<Node3D>(ScreenPath);
            if (TrayPath != null && !TrayPath.IsEmpty) _tray = GetNodeOrNull<Node3D>(TrayPath);

            _idling = new Idling(Card?.Idles ?? CompanionCard.IdlesByDefault, _rng);

            _speaking = OpenTheBank();

            GD.Print($"companion {(Card == null ? "(placeholder)" : Card.ToString())}" +
                     (CanSpeak ? "" : " - no bark bank, so it watches and says nothing"));
        }

        CompanionCard Look()
        {
            if (CompanionId.Length == 0) return null;

            CompanionCard card = Game.Campaigns.Library.Load(quiet: true).CompanionOf(CompanionId);

            if (card == null)
                GD.PushError($"companion: there is no '{CompanionId}' on the shelf - a placeholder " +
                             "sits on the table instead, which idles and says nothing");

            return card;
        }

        Speaking OpenTheBank()
        {
            if (Speaker.Length == 0) return null;

            BarkBank bank = Game.Campaigns.Library.Load(quiet: true).BarksFor(Speaker);

            // a companion with no bank is not an error: a campaign may ship a creature before its lines
            return bank?.Open(_rng);
        }

        // a body and a head, which is all the silhouette an idle needs from this camera - the same
        // argument DmHands makes for a forearm and a palm
        void Shape()
        {
            if (_body != null) return;

            var paint = new StandardMaterial3D
            {
                AlbedoColor = Coat,
                Roughness = 0.85f,
            };

            _body = new Node3D { Name = "Body" };

            var flank = new MeshInstance3D
            {
                Name = "Flank",
                Mesh = new BoxMesh { Size = new Vector3(0.048f, 0.026f, 0.086f) },
                MaterialOverride = paint,
                Position = new Vector3(0f, 0.013f, 0f),
            };

            _body.AddChild(flank);

            _head = new Node3D { Name = "Head", Position = new Vector3(0f, 0.022f, 0.048f) };

            var skull = new MeshInstance3D
            {
                Name = "Skull",
                Mesh = new BoxMesh { Size = new Vector3(0.026f, 0.022f, 0.034f) },
                MaterialOverride = paint,
            };

            _head.AddChild(skull);
            _body.AddChild(_head);

            AddChild(_body);
        }


        // ---- what the table tells it -------------------------------------------------------

        // the dice are in the air
        public void Watches()
        {
            Feel(Mood.Watching);

            LookAt(_tray);
        }

        // a rattle behind the screen (D2). It turns toward the screen, which is the whole gesture
        public void HearsASecretRoll()
        {
            Feel(Mood.Alert);

            LookAt(_screen);

            Say(Bark.Secret);
        }

        // the throw has landed. A Snag is cosmetic and is exactly what this creature is for
        public void Sees(Core.Resolution.PoolResult roll)
        {
            if (TableCues.For(roll) is not { } situation) { Settle(); return; }

            Feel(TableCues.MoodFor(situation));

            Say(situation);
        }

        public void SeesNerveSpent() { Feel(Mood.Alert); Say(Bark.Nerve); }

        public void SeesTheHeroDown() { Feel(Mood.Quiet); Say(Bark.Down); }

        public void SeesTheRoomCleared() { Feel(Mood.Pleased); Say(Bark.Victory); }

        // looks at YOU, which is the reaction THE_TABLE.md asks for before an unwise move
        public void LooksAtYou()
        {
            Feel(Mood.Alert);

            LookAt(null);
        }

        public void Settle()
        {
            _mood = Mood.Calm;
            _holding = 0.0;

            Straighten();
        }


        // ---- speaking ---------------------------------------------------------------------

        // the key it said, or null when this voice has nothing for the moment. Silence is a real
        // answer: forty barks for a Snag and none for a victory is a perfectly good bank.
        public string Say(Bark situation)
        {
            if (_speaking == null) return null;

            string key = _speaking.Next(situation);

            return key == null ? null : Line(key);
        }

        // says a line by key - a beat, a hint rung, a readout. The bark bank is not consulted:
        // the caller already knows which line it wants said.
        public string Line(string key, params object[] numbers)
        {
            if (string.IsNullOrEmpty(key)) return null;

            string said = numbers is { Length: > 0 } ? _text.Format(key, numbers) : _text.Get(key);

            // the clip is additive and almost always absent; the text does not wait on it and is
            // never cut short by it (W6)
            AudioStream clip = Voiceover.For(Speaker, key);
            double seconds = 0.0;

            if (clip != null)
            {
                var player = new AudioStreamPlayer3D { Name = "Voice", Stream = clip, Autoplay = false };

                AddChild(player);
                player.Finished += player.QueueFree;
                player.Play();

                seconds = clip.GetLength();
            }

            _bubble?.Say(said, Reading.Time(said, seconds));

            _spoken.Add(key);

            _idling?.Break();

            GD.Print($"        {Speaker}: \"{said}\"");

            return key;
        }

        public void Hush() => _bubble?.Clear();


        // ---- being alive ------------------------------------------------------------------

        void Feel(Mood mood)
        {
            if (!TableCues.Outranks(mood, _mood)) return;

            _mood = mood;
            _holding = TableCues.HoldFor(mood);

            _idling?.Break();
        }

        void LookAt(Node3D at)
        {
            if (_head == null) return;

            float turn = 0f;

            if (at != null)
            {
                Vector3 to = _head.ToLocal(at.GlobalPosition);

                turn = Mathf.Clamp(Mathf.Atan2(to.X, Mathf.Max(0.001f, Mathf.Abs(to.Z))), -Turn, Turn);
            }

            Swing(turn);
        }

        // toward the player is straight up the table, which from this head is a tilt and not a yaw
        void Straighten() => Swing(0f);

        void Swing(float to)
        {
            _turning?.Kill();
            _turning = CreateTween();
            _turning.SetTrans(Tween.TransitionType.Sine);
            _turning.SetEase(Tween.EaseType.Out);
            _turning.TweenProperty(_head, "rotation:y", to, TurnSeconds);
        }

        public override void _Process(double delta)
        {
            if (_holding > 0.0)
            {
                _holding -= delta;

                if (_holding <= 0.0) Settle();
            }

            if (_idling == null) return;

            if (!_idling.Tick(delta)) return;

            Breathe();
        }

        // the placeholder's idle: a small shift of weight, because a thing that never moves at all
        // reads as scenery and the whole point is that it does not
        void Breathe()
        {
            if (_body == null) return;

            float lean = (_idling.Idle % 5 - 2) * 0.012f;
            float rise = _mood == Mood.Quiet ? 0f : 0.002f;

            _shifting?.Kill();
            _shifting = CreateTween();
            _shifting.SetTrans(Tween.TransitionType.Sine);
            _shifting.SetEase(Tween.EaseType.InOut);
            _shifting.TweenProperty(_body, "position", new Vector3(lean, rise, 0f), 0.9f);
        }

        // a node that goes away takes its tweens with it; left to finish, they outlive the table
        public override void _ExitTree()
        {
            _shifting?.Kill();
            _turning?.Kill();
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{(Speaker.Length > 0 ? Speaker : "placeholder")} on the " +
            $"{(Card?.Perch ?? Perch.MapEdge).Word()}, {_mood.ToString().ToLowerInvariant()}, " +
            $"{_idling}";
    }
}

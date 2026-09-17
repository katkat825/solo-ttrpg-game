using Content.Places;
using Godot;

namespace Game.Dm
{
    [GlobalClass]
    public partial class DmHands : Node3D
    {
        [Export] public Vector3 Behind { get; set; } = new Vector3(0f, 0.02f, -0.16f);

        [Export] public float Reach { get; set; } = 0.55f;

        [Export] public float Back { get; set; } = 0.45f;

        // the pause where a hand stops over the square before it commits; the number most worth tuning
        [Export] public float Hesitation { get; set; } = 0.6f;

        // a withdrawal is fast, because being caught is the whole gesture
        [Export] public float Snatch { get; set; } = 0.18f;

        [Export] public Color Skin { get; set; } = new Color("#8C6239");

        public Gesture? Doing { get; private set; }

        public bool Busy => Doing != null;

        // fired when a gesture finishes, so a caller can sequence two without guessing durations
        [Signal] public delegate void PerformedEventHandler(int gesture);

        // how far behind the hands may get: about one beat of a fight (two notes and a placement)
        public const int Backlog = 3;

        readonly System.Collections.Generic.Queue<(Gesture Gesture, bool Hesitant, Vector3? At)>
            _waiting = new System.Collections.Generic.Queue<(Gesture, bool, Vector3?)>();

        Node3D _left;
        Node3D _right;
        Tween _tween;

        public override void _Ready()
        {
            if (_left != null) return;

            _left = Hand("Left", -0.055f);
            _right = Hand("Right", 0.055f);
        }

        // a forearm and a flat palm, which is all the silhouette a gesture needs from this camera
        Node3D Hand(string name, float x)
        {
            var hand = new Node3D { Name = name, Position = Behind + new Vector3(x, 0f, 0f) };

            var palm = new MeshInstance3D
            {
                Name = "Palm",
                Mesh = new BoxMesh { Size = new Vector3(0.042f, 0.014f, 0.070f) },
                MaterialOverride = Paint(),
            };

            var arm = new MeshInstance3D
            {
                Name = "Forearm",
                Mesh = new BoxMesh { Size = new Vector3(0.036f, 0.030f, 0.115f) },
                Position = new Vector3(0f, 0.008f, -0.092f),
                MaterialOverride = Paint(),
            };

            hand.AddChild(palm);
            hand.AddChild(arm);
            AddChild(hand);

            return hand;
        }

        StandardMaterial3D Paint() => new StandardMaterial3D
        {
            AlbedoColor = Skin,
            Roughness = 0.9f,
            Metallic = 0f,
        };

        // at is where on the table it happens, for gestures that need a square; a gesture asked while one runs waits (see the queue)
        public void Perform(Gesture gesture, bool hesitant = false, Vector3? at = null)
        {
            // queue rather than drop: a dropped second gesture once lost the guard reveal that followed a throw read-back
            // bounded: past the cap the oldest waiting gesture is dropped and logged, so a cue storm can't grow the queue forever
            if (Busy)
            {
                if (_waiting.Count >= Backlog)
                {
                    GD.Print($"dm      hands are {Backlog} behind - {gesture} dropped");
                    return;
                }

                _waiting.Enqueue((gesture, hesitant, at));
                return;
            }

            if (_left == null) _Ready();

            Doing = gesture;

            Vector3 target = at ?? Table(gesture);

            _tween?.Kill();
            _tween = CreateTween();

            switch (gesture)
            {
                // out, a pause if hesitant, down, and back; the pause is the gesture
                case Gesture.Place:
                    Out(_right, target + new Vector3(0f, 0.045f, 0f), Reach);
                    if (hesitant) _tween.TweenInterval(Hesitation);
                    Out(_right, target, 0.22f);
                    _tween.TweenInterval(0.12f);
                    Home(_right, Back);
                    break;

                // a flat hand pushing a tile in from the DM's side of the table
                case Gesture.Slide:
                    Out(_left, target + new Vector3(0f, 0.01f, -0.09f), Reach * 0.7f);
                    Out(_left, target + new Vector3(0f, 0.01f, 0f), Reach);
                    Home(_left, Back);
                    break;

                // across the table toward the player but stops short, so the note is left where you reach for it
                case Gesture.Push:
                case Gesture.Tack:
                    Out(_right, target, Reach);
                    _tween.TweenInterval(0.20f);
                    Home(_right, Back);
                    break;

                // twice, quickly; one tap is an accident
                case Gesture.Tap:
                    Out(_right, target + new Vector3(0f, 0.02f, 0f), Reach);
                    Out(_right, target, 0.10f);
                    Out(_right, target + new Vector3(0f, 0.014f, 0f), 0.10f);
                    Out(_right, target, 0.09f);
                    Home(_right, Back);
                    break;

                // behind the screen and back with nothing, built out of a pause where an object should have been
                case Gesture.ReachBehind:
                    Out(_right, Behind + new Vector3(0.02f, 0f, -0.05f), 0.35f);
                    _tween.TweenInterval(0.75f);
                    Home(_right, 0.5f);
                    break;

                case Gesture.Rest:
                    Out(_left, target, Reach * 1.3f);
                    break;

                // fast, and back further than it started - caught
                case Gesture.Withdraw:
                    Home(_left, Snatch);
                    Home(_right, Snatch);
                    break;

                case Gesture.TurnPage:
                    Out(_left, target + new Vector3(0.05f, 0.01f, 0f), Reach * 0.8f);
                    Out(_left, target + new Vector3(-0.05f, 0.01f, 0f), 0.30f);
                    Home(_left, Back);
                    break;

                case Gesture.Write:
                    Out(_right, target, Reach);
                    _tween.TweenInterval(0.45f);
                    Home(_right, Back);
                    break;

                default:
                    Home(_left, Back);
                    Home(_right, Back);
                    break;
            }

            _tween.TweenCallback(Callable.From(() =>
            {
                Gesture done = Doing ?? Gesture.Idle;

                Doing = null;

                EmitSignal(SignalName.Performed, (int)done);

                if (_waiting.Count == 0) return;

                (Gesture next, bool wasHesitant, Vector3? where) = _waiting.Dequeue();

                Perform(next, wasHesitant, where);
            }));
        }

        void Out(Node3D hand, Vector3 to, float seconds) =>
            _tween.TweenProperty(hand, "position", to, seconds).SetTrans(Tween.TransitionType.Sine);

        void Home(Node3D hand, float seconds) =>
            _tween.TweenProperty(hand, "position", hand == _left
                                     ? Behind + new Vector3(-0.055f, 0f, 0f)
                                     : Behind + new Vector3(0.055f, 0f, 0f), seconds)
                  .SetTrans(Tween.TransitionType.Sine);

        // default position when no square was given: toward the player for delivering gestures, the DM's side for the rest
        Vector3 Table(Gesture gesture) => gesture switch
        {
            Gesture.Push or Gesture.Write => new Vector3(0f, 0.01f, 0.14f),
            Gesture.Tack => new Vector3(0f, 0.12f, -0.10f),
            Gesture.TurnPage => new Vector3(0f, 0.01f, 0.06f),
            Gesture.Rest => new Vector3(-0.09f, 0.01f, 0.02f),
            _ => new Vector3(0f, 0.01f, 0f),
        };
    }
}

using Godot;

namespace Game.Room
{
    // THE CAMERA, DOING WHAT A PERSON AT A TABLE DOES.
    //
    // It replaces nothing about where the table is seen from: the overview is wherever the scene
    // put the camera, read once at boot and returned to whenever you look away. What it adds is
    // the two things you could not do before - lean over a fixed thing to read it, and pick a
    // handheld thing up - plus the mat's own pan and zoom for a place bigger than the table.
    //
    // Lean is the movement and Looking is the pan and zoom; this is the node that does as they
    // say. Nothing here is a control: there is no slider, no handle and no widget, and the only
    // input is reaching for an object or moving your head.
    [GlobalClass]
    public partial class Leaning : Node3D
    {
        [Export] public NodePath CameraPath { get; set; }

        // the mat, for the pan and zoom; unset leaves both off, which is right for a table whose
        // place always fits
        [Export] public NodePath BoardPath { get; set; }

        [Export] public float ZoomStep { get; set; } = 0.35f;

        [Export] public float PanSpeed { get; set; } = 0.35f;

        // how close the eye gets to a fixed thing it is leaning over
        [Export] public float Near { get; set; } = Lean.Nearest;

        [Signal] public delegate void PickedUpEventHandler(string what);

        Camera3D _camera;

        Board.Board _board;

        Transform3D _overview;

        Lean _lean;

        float _at;

        Looking _looking;

        // the thing in your hands, and where it was lying before you picked it up
        Node3D _held;

        Transform3D _lay;

        public bool Moving => _lean != null;

        public Node3D Held => _held;

        public Looking Window => _looking;

        public Transform3D Overview => _overview;

        public override void _Ready()
        {
            _camera = CameraPath != null && !CameraPath.IsEmpty
                ? GetNodeOrNull<Camera3D>(CameraPath)
                : GetViewport()?.GetCamera3D();

            if (_camera == null)
            {
                GD.PushWarning("leaning: there is no camera to lean, so the table is seen from " +
                               "wherever it was and nothing moves");
                return;
            }

            _overview = _camera.GlobalTransform;

            if (BoardPath != null && !BoardPath.IsEmpty)
                _board = GetNodeOrNull<Board.Board>(BoardPath);

            // a mat swapped for a bigger one is a mat with more to pan across, so the window is
            // measured again every time one goes down rather than once at boot
            if (_board != null) _board.LaidOut += Measure;

            Measure();

            SetProcess(true);
        }

        // the mat's extent in metres, so pan and zoom know what they are moving across
        public void Measure()
        {
            if (_board?.Metrics == null) { _looking = null; return; }

            _looking = new Looking(new Vector2(_board.Metrics.Width, _board.Metrics.Depth));

            GD.Print($"camera  {_looking}");
        }

        // ---- leaning --------------------------------------------------------------------------

        // over a thing that stays where it is: the mat, the screen, the corkboard
        public void LeanOver(Vector3 thing)
        {
            if (_camera == null) return;

            _lean = Lean.Toward(_camera.GlobalTransform, thing, Near);
            _at = 0f;
        }

        public void LeanOver(Node3D thing)
        {
            if (thing != null) LeanOver(thing.GlobalTransform.Origin);
        }

        // A HANDHELD THING COMES UP IN YOUR HANDS, and the camera does not move for it. Picking a
        // second thing up puts the first one back, because you have one pair of hands
        public bool PickUp(Node3D thing)
        {
            if (_camera == null || thing == null) return false;

            PutDown();

            _held = thing;
            _lay = thing.GlobalTransform;

            thing.GlobalTransform = new Transform3D(
                _camera.GlobalTransform.Basis,
                Lean.InHand(_camera.GlobalTransform));

            GD.Print($"camera  picked up {thing.Name}");

            EmitSignal(SignalName.PickedUp, thing.Name.ToString());

            return true;
        }

        public bool PutDown()
        {
            if (_held == null) return false;

            if (IsInstanceValid(_held)) _held.GlobalTransform = _lay;

            _held = null;

            return true;
        }

        // looking away puts everything back: the eye to the overview and whatever you were holding
        // back where it was lying
        public void Back()
        {
            PutDown();

            if (_camera == null) return;

            _lean = Lean.Away(_camera.GlobalTransform, _overview);
            _at = 0f;

            _looking?.Back();
        }


        // ---- the mat's own pan and zoom ---------------------------------------------------------

        public void Zoom(float by)
        {
            if (_looking == null || _board == null) return;

            _looking.ZoomBy(by);

            Follow();
        }

        public void Pan(Vector2 by)
        {
            if (_looking == null || _board == null || _looking.Fits) return;

            _looking.PanBy(by);

            Follow();
        }

        // the window Looking describes, turned into where the eye sits. A lean in flight wins,
        // because a lean is something you asked for and a pan is something you are doing
        void Follow()
        {
            if (_camera == null || _lean != null || _board == null) return;

            Vector3 middle = _board.ToGlobal(_looking.Middle);

            float near = Mathf.Lerp(FullMat(), Near, (_looking.Zoom - Looking.Widest) /
                                                     (Looking.Closest - Looking.Widest));

            _camera.GlobalTransform = new Transform3D(
                _overview.Basis, Lean.Over(_overview, middle, near));
        }

        // how far back the whole mat is seen from - the distance the overview already sits at, so
        // zoomed all the way out is exactly where the table started
        float FullMat()
        {
            Vector3 middle = _board.ToGlobal(Vector3.Zero);

            return (_overview.Origin - middle).Length();
        }


        public override void _Process(double delta)
        {
            if (_camera == null || _lean == null) return;

            _at += (float)delta;

            _camera.GlobalTransform = _lean.At(_at);

            if (!_lean.IsDone(_at)) return;

            _lean = null;
            _at = 0f;
        }

        // the keys, for a table played without a mouse. Every one of them is something you do with
        // your head, and none of them opens anything
        public override void _UnhandledInput(InputEvent @event)
        {
            if (_camera == null || @event is not InputEventKey { Pressed: true, Echo: false } key)
                return;

            switch (key.Keycode)
            {
                case Key.Escape: Back(); break;
                case Key.Equal: Zoom(ZoomStep); break;
                case Key.Minus: Zoom(-ZoomStep); break;
                case Key.Left: Pan(new Vector2(-PanSpeed, 0f)); break;
                case Key.Right: Pan(new Vector2(PanSpeed, 0f)); break;
                case Key.Up: Pan(new Vector2(0f, -PanSpeed)); break;
                case Key.Down: Pan(new Vector2(0f, PanSpeed)); break;
                default: return;
            }

            GetViewport().SetInputAsHandled();
        }

        public override void _ExitTree()
        {
            if (_board != null) _board.LaidOut -= Measure;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            _camera == null ? "leaning: no camera"
          : _held != null ? $"leaning: holding {_held.Name}"
          : _lean != null ? "leaning: " + _lean
          : "leaning: " + (_looking?.ToString() ?? "at the table");
    }
}

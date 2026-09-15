using System;
using System.Collections.Generic;
using Godot;
using Content.Minis;
using Game.Audio;

namespace Game.Board
{
    // grid holds the destination before the slide starts, so an interrupted move never leaves model and view disagreeing
    public partial class Mini : Node3D
    {
        [Export] public NodePath VoicePath { get; set; } = "Voice";

        // height is stated, scale is derived, so swapping the model keeps the piece the right height
        [Export] public NodePath FigurePath { get; set; } = "Figure";

        // 75 mm on a 60 mm square, taller than walls so a piece is never hidden
        [Export] public float FigureHeight { get; set; } = 0.075f;

        [Export] public Material Paint { get; set; }

        public MiniVoice Voice { get; set; } = MiniVoice.Shared;

        // null for base-game and clip-less pieces; each motion asks this and falls back to procedural, no pack branch anywhere
        public MiniClips Clips { get; set; }

        public PackVoice Foley { get; set; }

        AudioStreamPlayer3D _player;

        MiniStep _step;

        float _elapsed;

        public bool IsMoving => _step != null;

        // fires on a real move, not a refusal; the door check waits on this
        public event Action Arrived;

        bool _refusing;

        public override void _Ready()
        {
            _player = GetNodeOrNull<AudioStreamPlayer3D>(VoicePath);

            if (_player == null)
                GD.PushError($"mini: no AudioStreamPlayer3D at '{VoicePath}' - it will be set down in silence");

            Stand(GetNodeOrNull<Node3D>(FigurePath));
        }

        // feet stay on the base whatever the model's origin, so a model built in a hole still stands right
        void Stand(Node3D figure)
        {
            if (figure == null)
            {
                GD.PushError($"mini: no figure at '{FigurePath}' - the piece is a base with nothing on it");
                return;
            }

            if (Paint == null)
                GD.PushError("mini: the figure has no paint - it will wear whatever its pack came with");

            PaintedModel.Paint(figure, Paint);

            Aabb bounds = PaintedModel.Bounds(figure);
            float scale = PaintedModel.ToFitHeight(bounds, FigureHeight);

            figure.Scale = Vector3.One * scale;

            // only the height is corrected: centring the bounding box would push an off-centre figure's feet off the square
            figure.Position = new Vector3(
                figure.Position.X,
                figure.Position.Y - bounds.Position.Y * scale,
                figure.Position.Z);

            GD.Print($"mini    {figure.Name} {bounds.Size} units -> {FigureHeight * 1000f:0} mm tall");
        }

        public void PlaceAt(Vector3 boardLocal)
        {
            _step = null;
            _elapsed = 0f;
            Position = boardLocal;

            Clips?.Play(Motion.Placed);
        }

        // the whole route as one movement, starting from where it is now, so a mid-move click retargets and the first waypoint is replaced
        public void Follow(IReadOnlyList<Vector3> route)
        {
            if (route == null || route.Count == 0) return;

            var through = new List<Vector3>(route) { [0] = Position };

            _step = new MiniStep(through);
            _elapsed = 0f;
            _refusing = false;

            Clips?.Play(Motion.Move);
        }

        // view only; the board decides whether to refuse, and the piece ends exactly where it started
        public void Refuse(Vector3 toward) => Lean(toward);

        // same motion as a refusal, but named apart so a swing does not read as a refused move
        public void Strike(Vector3 toward)
        {
            Clips?.Play(Motion.Strike);

            Lean(toward);
        }

        // ignored mid-move and when toppled: interrupting a carry or reacting on a corpse reads as a glitch
        public void Wobble()
        {
            if (_toppled) return;

            if (Clips != null && Clips.Play(Motion.Wobble)) return;

            if (IsMoving) return;

            Lean(Position + new Vector3(0f, 0f, FigureHeight * 0.22f));
        }

        void Lean(Vector3 toward)
        {
            _step = MiniStep.Refusing(Position, toward);
            _elapsed = 0f;
            _refusing = true;
        }

        // view only, laid on its side and left; the grid already removed it (Board.Lift)
        public void Topple()
        {
            if (_toppled) return;

            _toppled = true;
            _step = null;

            // a death clip if the model has one, else the procedural tip-over every piece has used since
            if (Clips != null && Clips.Play(Motion.Topple))
            {
                Foley?.Play(_player, Motion.Topple);
                return;
            }

            Foley?.Play(_player, Motion.Topple);

            // lifted as it rotates about the feet, or the toppled piece lies half inside the mat
            RotateX(-Mathf.Pi * 0.5f);
            Position += new Vector3(0f, FigureHeight * 0.16f, 0f);
        }

        public bool IsToppled => _toppled;

        bool _toppled;

        public override void _Process(double delta)
        {
            if (_step == null) return;

            float was = _elapsed;
            _elapsed += (float)delta;

            Position = _step.At(_elapsed);

            // the pack's own foley first, else the shared pool, no branch on where the piece came from
            if (_step.SetsDownBetween(was, _elapsed) &&
                !(Foley?.Play(_player, Motion.Placed) ?? false))
                Voice?.SetDown(_player);

            if (!_step.IsDone(_elapsed)) return;

            _step = null;

            // cleared before Arrived fires: a listener may start the next move inside it
            bool refusal = _refusing;
            _refusing = false;

            if (!refusal) Arrived?.Invoke();
        }
    }
}

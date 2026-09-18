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


        // TAKEN OFF THE TABLE (the eye check, 2026-09-16). A toppled piece used to lie where it
        // fell for the rest of the fight - "freed from the grid but left toppled where it fell" -
        // and by the third round the board was more bodies than squares.
        //
        // The fall still registers: it topples, it LIES there for a beat, and then it goes. What
        // changed is WHO takes it: nothing lifts itself off the table any more, and the DM's hand
        // never sweeps (five deaths in a round is five queued gestures). The companion huffs at it
        // and it SKITTERS - away from the creature, off the edge of the mat, gone. A body that
        // slides away from something that just barked at it reads as cleared; a body that rises
        // straight up reads as a bug.
        //
        // It is HIDDEN rather than freed. A Piece holds this node and a save rebuilds the fight
        // from the actors, so freeing it here would leave a dangling reference for the sake of one
        // object that costs nothing to keep.

        // toppled and left there, so the fall is seen before the huff comes for it
        public const double ClearedAfter = 0.8;

        // and then away, quicker than a hand would have been, because nothing is carrying it
        public const double ClearedOver = 0.3;

        // how far it slides before it is out of sight, in figure heights
        public const float SkitterReach = 2.2f;

        double _clearing = -1.0;

        Vector3 _clearedFrom;

        Vector3 _skitter = Vector3.Zero;

        public bool BeingCleared => _clearing >= 0.0;

        // away from whatever huffed at it, flat across the felt; straight back when nothing did
        public void Skitter(Vector3 shooedFrom)
        {
            if (!_toppled || _clearing >= 0.0 || !Visible) return;

            Vector3 away = Position - shooedFrom;
            away.Y = 0f;

            _skitter = away.LengthSquared() < 0.000001f
                ? Vector3.Forward
                : away.Normalized();

            _clearing = ClearedAfter + ClearedOver;
            _clearedFrom = Position;
        }

        // counted down in _Process rather than ended with a tween callback, for the reason
        // Bubble.cs spells out at its own line: a Callable.From(...) holds a C# delegate alive on
        // Godot's side, and one per piece killed is one per piece leaked
        void Clearing(double delta)
        {
            _clearing -= delta;

            if (_clearing <= 0.0)
            {
                _clearing = -1.0;
                Visible = false;
                return;
            }

            if (_clearing >= ClearedOver) return;

            float gone = 1f - (float)(_clearing / ClearedOver);

            // it goes fast at first and then is simply not there; a piece that decelerates to a
            // stop off the mat is a piece you watched being deleted
            Position = _clearedFrom + _skitter * (gone * FigureHeight * SkitterReach);
        }

        public override void _Process(double delta)
        {
            if (_clearing >= 0.0) { Clearing(delta); return; }

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

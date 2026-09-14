using System;
using System.Collections.Generic;
using Godot;
using Game.Audio;

namespace Game.Board
{
    // one piece on the board: where it is standing, and how it gets to the next square.
    //
    // Thin on purpose, the way DiceTray is thin. It owns the clock and the mesh and nothing else -
    // MiniStep owns the shape of the move, MiniVoice owns what the set-down sounds like, and
    // Core.Space.Grid owns which square it is on. THE MODEL SAYS WHICH CELL, THE VIEW ANIMATES
    // GETTING THERE (THE_BOARD.md B1): by the time this is told to slide, the grid already has
    // the piece on the destination, so an interrupted move can never leave the two disagreeing.
    //
    // EVERYTHING HERE IS IN THE BOARD'S OWN SPACE. A mini is a child of the board node, so its
    // Position is board-local and BoardMetrics.Centre hands over exactly the number to use. Same
    // discipline as the tray since F2: nothing measures from the world origin, so the board can
    // stand anywhere on the table.
    public partial class Mini : Node3D
    {
        [Export] public NodePath VoicePath { get; set; } = "Voice";

        // THE FIGURE (B5). A model out of an asset pack, sized and painted here rather than baked
        // into the scene at a scale somebody worked out once - swap the model in mini.tscn and the
        // piece is still the right height, because the height is what is stated and the scale is
        // what is derived
        [Export] public NodePath FigurePath { get; set; } = "Figure";

        // 75 mm of miniature on a 60 mm square. taller than the walls on purpose: a piece that can
        // hide behind terrain is a piece you have to hunt for, and cover is Phase C's decision
        [Export] public float FigureHeight { get; set; } = 0.075f;

        // the painted-miniature shader, wearing this hero's atlas
        [Export] public Material Paint { get; set; }

        // what a piece sounds like set down. settable rather than looked up, so a heavier or
        // lighter piece is an assignment - the arrangement DieAudio.Voice already uses
        //
        // NOT an interface yet, and deliberately: IDieVoice earned one because a brass die and a
        // stone die are a real difference an ear can hear and BrassVoice is a file away. There is
        // one kind of piece on the board and no second implementation waiting to happen, so a
        // seam here would be abstracting past the point (CONVENTIONS.md 5). When pieces start
        // differing by material, this is where the interface goes
        public MiniVoice Voice { get; set; } = MiniVoice.Shared;

        AudioStreamPlayer3D _player;

        MiniStep _step;

        // seconds into the current move
        float _elapsed;

        public bool IsMoving => _step != null;

        // the piece finished a MOVE - not a refusal, which ends where it began and has nothing to
        // announce. B4 listens to this: the hero walks up to the door and only then do the dice
        // come out, because that is the order it happens at a table
        public event Action Arrived;

        // true while the current step is a refusal
        bool _refusing;

        public override void _Ready()
        {
            _player = GetNodeOrNull<AudioStreamPlayer3D>(VoicePath);

            if (_player == null)
                GD.PushError($"mini: no AudioStreamPlayer3D at '{VoicePath}' - it will be set down in silence");

            Stand(GetNodeOrNull<Node3D>(FigurePath));
        }

        // the figure, painted and cut down to size. its own feet stay on the base, whatever the
        // model's origin happens to be - a pack that models its characters standing in a hole is
        // a pack, not a bug to find at the table
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

            // ONLY the height is corrected. A model is centred on its own origin by whoever made
            // it, and a figure holding an axe out to one side has a bounding box that is not: put
            // the BOX in the middle of the square and the FEET end up off it, which is the one
            // thing a based miniature must never look like
            figure.Position = new Vector3(
                figure.Position.X,
                figure.Position.Y - bounds.Position.Y * scale,
                figure.Position.Z);

            GD.Print($"mini    {figure.Name} {bounds.Size} units -> {FigureHeight * 1000f:0} mm tall");
        }

        // straight there, no choreography - how a piece ARRIVES on the board, as opposed to how
        // it moves once it is there. B0's placement and, later, a campaign's spawn points
        public void PlaceAt(Vector3 boardLocal)
        {
            _step = null;
            _elapsed = 0f;
            Position = boardLocal;
        }

        // the way round, square by square, as ONE movement - lifted once, traced round the wall,
        // set down at the far end.
        //
        // FROM WHERE IT IS NOW, not from the square it was told to leave. a second click during a
        // move retargets rather than being swallowed: a piece that ignores you mid-slide feels
        // broken, and one that snaps back to re-start feels worse. the route the board hands over
        // starts at the square being left, so the first waypoint is replaced rather than prepended
        public void Follow(IReadOnlyList<Vector3> route)
        {
            if (route == null || route.Count == 0) return;

            var through = new List<Vector3>(route) { [0] = Position };

            _step = new MiniStep(through);
            _elapsed = 0f;
            _refusing = false;
        }

        // a square it cannot have: lean at it, think better of it, settle back.
        //
        // the board decides WHETHER a move is refused; this is only what that looks like. the piece
        // ends exactly where it started, which MiniStep.Refusing guarantees by building a path that
        // returns to its own beginning rather than by being careful
        public void Refuse(Vector3 toward) => Lean(toward);

        // a swing: reach at the thing and come back. THE SAME MOVEMENT AS A REFUSAL, and that is
        // not a shortcut - both are a piece reaching at a square it does not end up standing on,
        // which is exactly what a hand does at a table for either. The two words are kept apart
        // because the call sites mean different things and reading `Refuse` at a swing would send
        // the next reader looking for a rule that refused it
        public void Strike(Vector3 toward) => Lean(toward);

        void Lean(Vector3 toward)
        {
            _step = MiniStep.Refusing(Position, toward);
            _elapsed = 0f;
            _refusing = true;
        }

        // OFF ITS FEET. A downed piece is laid on its side and left there, the way one is at a
        // table - taken off the board is a separate act, and seeing where somebody fell is worth
        // more than a tidy map. It stops being a piece that can be clicked because the grid no
        // longer has it (Board.Lift does that); this is only what it looks like
        public void Topple()
        {
            if (_toppled) return;

            _toppled = true;
            _step = null;

            // laid out away from the near edge of the table, so it reads as fallen from the one
            // fixed camera rather than as a piece standing at a strange angle. lifted by about
            // half a figure's thickness at the same time: the rotation is about the feet, and
            // without this the piece lies half inside the mat
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

            // the click goes with the placement, not with the arrival at the square - the piece
            // has been over its destination for the whole hover
            if (_step.SetsDownBetween(was, _elapsed)) Voice?.SetDown(_player);

            if (!_step.IsDone(_elapsed)) return;

            _step = null;

            // cleared BEFORE the announcement, because a listener is entitled to start the next
            // move inside it and would otherwise have its flag stamped on afterwards
            bool refusal = _refusing;
            _refusing = false;

            if (!refusal) Arrived?.Invoke();
        }
    }
}

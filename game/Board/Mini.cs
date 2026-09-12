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

        public override void _Ready()
        {
            _player = GetNodeOrNull<AudioStreamPlayer3D>(VoicePath);

            if (_player == null)
                GD.PushError($"mini: no AudioStreamPlayer3D at '{VoicePath}' - it will be set down in silence");
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
        }

        // a square it cannot have: lean at it, think better of it, settle back.
        //
        // the board decides WHETHER a move is refused; this is only what that looks like. the piece
        // ends exactly where it started, which MiniStep.Refusing guarantees by building a path that
        // returns to its own beginning rather than by being careful
        public void Refuse(Vector3 toward)
        {
            _step = MiniStep.Refusing(Position, toward);
            _elapsed = 0f;
        }

        public override void _Process(double delta)
        {
            if (_step == null) return;

            float was = _elapsed;
            _elapsed += (float)delta;

            Position = _step.At(_elapsed);

            // the click goes with the placement, not with the arrival at the square - the piece
            // has been over its destination for the whole hover
            if (_step.SetsDownBetween(was, _elapsed)) Voice?.SetDown(_player);

            if (_step.IsDone(_elapsed)) _step = null;
        }
    }
}

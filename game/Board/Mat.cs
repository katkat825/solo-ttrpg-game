using System;
using Content.Places;
using Godot;

namespace Game.Board
{
    // THE MAT: THE PLACE YOU ARE IN, AS AN OBJECT ON THE TABLE.
    //
    // Everything the board draws of a map - the felt, the grid lines, the walls and doors standing
    // on it, the square that flashes when a move is refused - hangs off this node and nothing
    // else does. So a place change is this one object being carried away and another laid down,
    // and the tray, the sheet, the screen and the companion never move.
    //
    // It owns no map and no rules. The board hands it a way to build the next mat and it runs the
    // choreography; MatSwap is the choreography and this is the thing being carried.
    [GlobalClass]
    public partial class Mat : Node3D
    {
        // where a mat rests when nobody is carrying it; set by whoever put the mat on the table
        public Vector3 Rests { get; set; } = Vector3.Zero;

        public bool Swapping => _swap != null;

        // the new mat is down and the board may be stood on again
        public event Action Laid;

        MatSwap _swap;

        float _at;

        Action _build;

        Action<Gesture> _hands;

        bool _built;

        // NOTHING STANDS ON A MAT THAT IS IN THE AIR. A caller asks this before it puts a piece
        // down, so a mini can never be set on a place that is half way across the table.
        public bool Ready => _swap == null;

        public void SwapFor(Action build, Action<Gesture> hands = null)
        {
            if (build == null) return;

            // a second place change while one is in the air. Finishing the pending build first is
            // the only answer that cannot leave two mats on the table at once
            if (_swap != null)
            {
                GD.Print("mat     a place changed while the last one was still being laid - " +
                         "the first mat goes down before the second is lifted");
                Settle();
            }

            _build = build;
            _hands = hands;
            _built = false;
            _at = 0f;
            _swap = new MatSwap();

            _hands?.Invoke(MatSwap.Lifts);

            SetProcess(true);
        }

        public override void _Process(double delta)
        {
            if (_swap == null) return;

            float was = _at;

            _at += (float)delta;

            if (_swap.LaysBetween(was, _at)) Build();

            Position = Rests + _swap.At(_at);

            if (!_swap.IsDone(_at)) return;

            Settle();
        }

        void Build()
        {
            if (_built) return;

            _built = true;
            _build?.Invoke();

            _hands?.Invoke(MatSwap.Lays);
        }

        void Settle()
        {
            Build();

            _swap = null;
            _build = null;
            _hands = null;
            _at = 0f;

            Position = Rests;

            SetProcess(false);

            Laid?.Invoke();
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            _swap == null ? "mat: down" : $"mat: swapping, {_at:0.00}s of {_swap.Duration:0.00}s";
    }
}

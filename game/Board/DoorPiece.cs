using Godot;

namespace Game.Board
{
    public partial class DoorPiece : Node3D
    {
        public const float SwingSeconds = 0.5f;

        // wide enough to read as open, short of flat against the wall
        public const float ForcedDegrees = -104f;

        // further and off the vertical: one hinge gone, panel leaning in
        public const float GivenDegrees = -122f;

        public const float GivenLeanDegrees = -15f;

        // in the leaf's own units, not metres: the piece is scaled to a 60 mm square
        public float Drop { get; set; } = -0.004f;

        Vector3 _fromRotation;

        Vector3 _toRotation;

        Vector3 _fromPosition;

        Vector3 _toPosition;

        float _elapsed = -1f;

        public bool IsOpen { get; private set; }

        public bool IsSwinging => _elapsed >= 0f;

        // the swinging part, hung at the hinge; set at build time or nothing opens
        public Node3D Leaf { get; set; }

        public void Open(bool cleanly)
        {
            if (IsOpen || Leaf == null) return;

            IsOpen = true;

            _fromRotation = Leaf.RotationDegrees;
            _fromPosition = Leaf.Position;

            _toRotation = cleanly
                ? new Vector3(0f, ForcedDegrees, 0f)
                : new Vector3(0f, GivenDegrees, GivenLeanDegrees);

            _toPosition = cleanly ? _fromPosition : _fromPosition + new Vector3(0f, Drop, 0f);

            _elapsed = 0f;
        }

        public override void _Process(double delta)
        {
            if (_elapsed < 0f || Leaf == null) return;

            _elapsed += (float)delta;

            float through = Mathf.Clamp(_elapsed / SwingSeconds, 0f, 1f);

            // ease-out: quick off the mark, slowing into the stop, like a door being shoved
            float eased = 1f - (1f - through) * (1f - through);

            Leaf.RotationDegrees = _fromRotation.Lerp(_toRotation, eased);
            Leaf.Position = _fromPosition.Lerp(_toPosition, eased);

            if (through >= 1f) _elapsed = -1f;
        }
    }
}

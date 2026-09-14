using Godot;

namespace Game.Board
{
    // the door, as a thing with a hinge.
    //
    // It is a Node3D standing on the square, with the LEAF hung off a hinge inside it: rotating
    // the hinge swings the door the way a door swings, and no animation has to know where the
    // panel is. B4 needed it because "the door opens" has to be something you SEE happen - a door
    // that vanishes the frame the dice settle is a state change, not a door.
    //
    // The frame does not move, which is why the leaf is a separate node rather than this one. B5
    // made that matter: a KayKit doorway is a wall with a hole in it and a door panel inside, and
    // swinging the wall would be a fine joke exactly once.
    //
    // Two ways open, and they are the point of the milestone: forced swings it wide and clean,
    // given drops it off its hinges and leaves it hanging. Told apart by silhouette from across
    // the table, which is the same test every other piece of terrain on this board has to pass.
    public partial class DoorPiece : Node3D
    {
        // a door swings about as fast as a hand pushes it
        public const float SwingSeconds = 0.5f;

        // wide enough to read as OPEN from the table's own angle, and stopping short of flat
        // against the wall so the panel stays visible rather than merging into it
        public const float ForcedDegrees = -104f;

        // further, and off the vertical: one hinge gone and the panel leaning into the doorway
        public const float GivenDegrees = -122f;

        public const float GivenLeanDegrees = -15f;

        // and dropped, because the top hinge is what tore out. IN THE LEAF'S OWN UNITS, which are
        // the model's rather than the table's - a door piece is scaled down to a 60 mm square, so
        // a number in metres here would be a drop of a tenth of a millimetre. Whoever builds the
        // door knows how tall it is and sets this from it
        public float Drop { get; set; } = -0.004f;

        Vector3 _fromRotation;

        Vector3 _toRotation;

        Vector3 _fromPosition;

        Vector3 _toPosition;

        float _elapsed = -1f;

        public bool IsOpen { get; private set; }

        public bool IsSwinging => _elapsed >= 0f;

        // the part that swings, hung at the hinge. everything else under this node is the frame
        // and stays where it is. set at build time; without one there is nothing to open
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

            // a door being shoved: quick off the mark and slowing into the stop, rather than the
            // symmetric ease a hand moving a piece gets
            float eased = 1f - (1f - through) * (1f - through);

            Leaf.RotationDegrees = _fromRotation.Lerp(_toRotation, eased);
            Leaf.Position = _fromPosition.Lerp(_toPosition, eased);

            if (through >= 1f) _elapsed = -1f;
        }
    }
}

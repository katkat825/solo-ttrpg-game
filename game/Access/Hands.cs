using Godot;

namespace Game.Access
{
    // YOUR OWN HANDS, ON YOUR SIDE OF THE TABLE (AX5).
    //
    // The DM has had hands since D1 and you have not, which meant the table had somebody else's
    // presence in it and none of yours. These are yours: a forearm and a flat palm each, resting at
    // the near edge, in whatever colour you chose - and gone entirely if you would rather they were.
    //
    // PLACEHOLDER GEOMETRY, DELIBERATELY, AND THE SAME PLACEHOLDER THE DM'S HANDS ARE. Rigged
    // expressive hands are the least-covered thing in the free asset libraries and are the strongest
    // candidate on the short list of things worth commissioning; until then a silhouette at the right
    // scale in the right colour is what there is, and it is enough to tell whether the choice works.
    //
    // What is NOT a placeholder is the choice itself: the range of colours, the two non-human ones,
    // and the visibility toggle are the point of the milestone, and they are all real here.
    [GlobalClass]
    public partial class Hands : Node3D
    {
        // how far apart, either side of where you are sitting
        [Export] public float Apart { get; set; } = 0.17f;

        [Export] public Vector3 Rest { get; set; } = new Vector3(0f, 0.005f, 0.06f);

        // laid on the table pointing away from you, so the palms read as palms from this camera
        [Export] public float Lean { get; set; } = 8f;

        public Access.Arm Skin { get; private set; } = Access.Arm.Almond;

        public Shown Showing { get; private set; } = Shown.Both;

        Node3D _left;

        Node3D _right;

        StandardMaterial3D _paint;

        public override void _Ready()
        {
            if (_left != null) return;

            _paint = new StandardMaterial3D { Metallic = 0f };

            _left = Arm("Left", -Apart * 0.5f);
            _right = Arm("Right", Apart * 0.5f);

            Wears(Skin, Showing);
        }

        // a forearm and a flat palm, which is all the silhouette this camera can see anyway - the
        // same shapes the DM's hands are built from, so the two sides of the table match
        Node3D Arm(string named, float x)
        {
            var arm = new Node3D
            {
                Name = named,
                Position = Rest + new Vector3(x, 0f, 0f),
                RotationDegrees = new Vector3(Lean, 0f, 0f),
            };

            arm.AddChild(new MeshInstance3D
            {
                Name = "Palm",
                Mesh = new BoxMesh { Size = new Vector3(0.046f, 0.015f, 0.076f) },
                MaterialOverride = _paint,
            });

            arm.AddChild(new MeshInstance3D
            {
                Name = "Forearm",
                Mesh = new BoxMesh { Size = new Vector3(0.040f, 0.032f, 0.130f) },
                Position = new Vector3(0f, 0.009f, 0.100f),
                MaterialOverride = _paint,
            });

            AddChild(arm);

            return arm;
        }

        // ONE CALL FOR BOTH HALVES OF THE CHOICE, because they are one choice: hidden arms have no
        // colour, and a colour nobody can see is not a setting
        public void Wears(Access.Arm skin, Shown showing)
        {
            Skin = skin;
            Showing = showing;

            if (_paint != null)
            {
                _paint.AlbedoColor = skin.Paint();
                _paint.Roughness = skin.Rough();
            }

            if (_left != null) _left.Visible = showing.Left();

            if (_right != null) _right.Visible = showing.Right();
        }

        public void Wears(Adjustments how)
        {
            if (how != null) Wears(how.Skin, how.Showing);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"hands: {Skin.Word()}, {Showing.Word()} ({Showing.Count()} visible)";
    }
}

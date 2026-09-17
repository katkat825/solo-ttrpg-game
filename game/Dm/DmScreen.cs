using Godot;

namespace Game.Dm
{
    [GlobalClass]
    public partial class DmScreen : Node3D
    {
        [Export] public float PanelHeight { get; set; } = 0.23f;

        [Export] public float PanelWidth { get; set; } = 0.20f;

        [Export] public float Thickness { get; set; } = 0.003f;

        [Export] public float FoldDegrees { get; set; } = 28f;

        [Export] public Color Card { get; set; } = new Color("#6B4A2F");

        [Export] public Color Inside { get; set; } = new Color("#3A2A1E");

        [Export] public Color Paper { get; set; } = new Color("#D8CFB8");

        [Export] public Color Pencil { get; set; } = new Color("#3B3A42");

        // idempotent: Godot re-runs _Ready on a reparent or editor reload, and building twice would stack two screens
        public override void _Ready()
        {
            if (GetNodeOrNull("Centre") != null) return;

            Build();
        }

        void Build()
        {
            Panel("Centre", Vector3.Zero, 0f, PanelWidth);

            float half = PanelWidth * 0.5f;
            float wing = PanelWidth * 0.62f;

            // THE WINGS FOLD AWAY FROM THE PLAYER, around the DM. A screen is concave on the
            // side the DM sits on - that is what it is for - and convex toward the table, and the
            // eye check found this built inside out: the wings came forward and cupped the PLAYER,
            // which reads as a screen with its back to you.
            //
            // -Z is behind it. DmHands.Behind says so at its own line, and everything the DM
            // produces comes from back there (DM_PRESENCE.md D0), so that is the direction the
            // wings have to sweep.
            Panel("Left", new Vector3(-half, 0f, 0f), -FoldDegrees, wing, hingeLeft: true);
            Panel("Right", new Vector3(half, 0f, 0f), FoldDegrees, wing, hingeLeft: false);

            Props();
        }

        // hinged at an edge, not centred: a wing pivoted about its middle leaves a gap at the fold you can see the DM through
        void Panel(string name, Vector3 hinge, float degrees, float width, bool hingeLeft = false)
        {
            var pivot = new Node3D { Name = name, Position = hinge };

            pivot.RotateY(Mathf.DegToRad(degrees));

            var mesh = new MeshInstance3D
            {
                Name = "Panel",
                Mesh = new BoxMesh { Size = new Vector3(width, PanelHeight, Thickness) },

                // the panel hangs off the hinge rather than straddling it, and stands on the table
                Position = new Vector3(hinge == Vector3.Zero ? 0f : (hingeLeft ? -width * 0.5f : width * 0.5f),
                                       PanelHeight * 0.5f, 0f),

                MaterialOverride = Paint(Card),
            };

            pivot.AddChild(mesh);
            AddChild(mesh.Owner = pivot);
        }

        // props on the inside edge, deliberately too small and edge-on to read from the camera.
        // Inside is -Z, the DM's side, which is the side the wings now close around.
        void Props()
        {
            var inside = new Node3D { Name = "Inside", Position = new Vector3(0f, 0f, -Thickness) };

            AddChild(inside);

            Note(inside, "Note1", new Vector3(-0.055f, 0.170f, 0f), 0.030f, 0.026f, -7f);
            Note(inside, "Note2", new Vector3(0.004f, 0.186f, 0f), 0.026f, 0.022f, 4f);
            Note(inside, "Note3", new Vector3(0.062f, 0.161f, 0f), 0.034f, 0.024f, -2f);

            Note(inside, "MapScrap", new Vector3(-0.012f, 0.108f, 0f), 0.070f, 0.050f, 2f, Inside);

            var pencil = new MeshInstance3D
            {
                Name = "Pencil",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.0022f,
                    BottomRadius = 0.0022f,
                    Height = 0.09f,
                    RadialSegments = 6,
                },
                Position = new Vector3(0.03f, 0.004f, -0.012f),
                MaterialOverride = Paint(Pencil),
            };

            pencil.RotateX(Mathf.DegToRad(90f));
            pencil.RotateZ(Mathf.DegToRad(-12f));

            inside.AddChild(pencil);
        }

        void Note(Node3D parent, string name, Vector3 at, float width, float height, float tilt,
                  Color? colour = null)
        {
            var note = new MeshInstance3D
            {
                Name = name,
                Mesh = new BoxMesh { Size = new Vector3(width, height, 0.0004f) },
                Position = at,
                MaterialOverride = Paint(colour ?? Paper),
            };

            note.RotateZ(Mathf.DegToRad(tilt));

            parent.AddChild(note);
        }

        static StandardMaterial3D Paint(Color colour) => new StandardMaterial3D
        {
            AlbedoColor = colour,

            // high roughness: a specular highlight on cardboard is the fastest way to make a prop look like a video game
            Roughness = 0.95f,
            Metallic = 0f,
        };
    }
}

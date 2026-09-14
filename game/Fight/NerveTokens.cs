using Godot;

namespace Game.Fight
{
    // HEROIC EFFORT, AS THINGS YOU CAN PUSH ACROSS THE TABLE (COMBAT_LOOP.md C4).
    //
    // Nerve is three at the start of a day and five at most (CORE_RULES.md section 7), and the
    // whole of its interest is that spending one is a DECISION - so it is a small pile of tokens
    // beside the hero's piece rather than a number in a corner. You can see how many you have
    // without reading anything, and spending one is picking one up.
    //
    // CLICKING A TOKEN IS THE GESTURE, and what it buys is decided by what the table is waiting
    // for: a Trouble on the felt means shrug it, the hero's own turn means push for one more
    // action or arm the Heart die. `Fight` owns that; this owns what a token looks like and where
    // it is. Deciding it here would put a rule in the drawing.
    //
    // ARMED IS A TOKEN STANDING UP. When a Nerve has been committed to the next throw - the Heart
    // die - one token lifts off the table and waits there, which is what somebody does with a chip
    // they have already decided to bet. It goes back down if the player changes their mind.
    //
    // Distinguishable from the vigor tally at a glance by shape and colour, not by position: a
    // stack of pale discs beside a row of small red strokes. Both are on the mat and neither is
    // a bar.
    public partial class NerveTokens : Node3D
    {
        // a chip, not a pip. Bigger than a vigor stroke because there are never more than five of
        // them and each one is a decision
        public const float Radius = 0.0042f;

        public const float Thickness = 0.0016f;

        public const float Pitch = 0.0105f;

        // the near side of the piece and the same side as the vigor tally, below it, so the two
        // read as one column of bookkeeping rather than as clutter on both flanks
        public static readonly Vector3 Beside = new Vector3(0.021f, 0f, -0.014f);

        public const float Lift = 0.0012f;

        // and how far a committed token stands off the table
        public const float Armed = 0.010f;

        // old brass. Warm, and nothing else on the board is this colour
        public Color Ink { get; set; } = new Color(0.86f, 0.72f, 0.36f);

        // spent ones stay as ghosts, for the same reason the vigor tally keeps its dead pips:
        // "one left of three" and "one left of five" are different situations
        public Color Spent { get; set; } = new Color(0.34f, 0.31f, 0.26f, 0.30f);

        // A TOKEN IS A THING YOU CAN TOUCH, so it has a body and not only a mesh. Everything else
        // on the board is clicked by meeting the mat's own plane (Board.CellUnder), which is exact
        // and needs no physics - but a token is a small object standing ON a square rather than
        // being one, and a click on it must not read as a click on the square underneath. So it is
        // met the way a die is met: with its own collision shape (DiceTray.DieUnder)
        StaticBody3D[] _tokens;

        StandardMaterial3D _full;

        StandardMaterial3D _empty;

        public int Cap { get; private set; }

        public int Showing { get; private set; }

        // how many are committed and standing up, waiting on the next throw
        public int Standing { get; private set; }

        public void Track(int cap)
        {
            Cap = cap < 0 ? 0 : cap;
            Showing = 0;
            Standing = 0;

            Build();
        }

        void Build()
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }

            _full = Unshaded(Ink);
            _empty = Unshaded(Spent);

            var chip = new CylinderMesh
            {
                TopRadius = Radius,
                BottomRadius = Radius,
                Height = Thickness,
                RadialSegments = 16,
                Rings = 1,
            };

            // a shade wider than the chip, so a token is easy to hit at the table's 60 degrees
            var reach = new CylinderShape3D { Radius = Radius * 1.6f, Height = Thickness * 6f };

            _tokens = new StaticBody3D[Cap];
            _faces = new MeshInstance3D[Cap];

            for (int i = 0; i < Cap; i++)
            {
                var token = new StaticBody3D
                {
                    Name = $"Nerve{i}",
                    Position = Beside + new Vector3(i * Pitch, Lift, 0f),
                };

                token.AddChild(_faces[i] = new MeshInstance3D
                {
                    Name = "Chip",
                    Mesh = chip,
                    MaterialOverride = _empty,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });

                token.AddChild(new CollisionShape3D { Name = "Reach", Shape = reach });

                _tokens[i] = token;
                AddChild(token);
            }
        }

        MeshInstance3D[] _faces;

        // is this what the ray met? asked by `Fight`, which owns what a click on one MEANS -
        // deciding that here would put a rule in the drawing
        public bool Owns(GodotObject collider)
        {
            if (_tokens == null || collider == null) return false;

            foreach (StaticBody3D token in _tokens)
                if (ReferenceEquals(token, collider)) return true;

            return false;
        }

        static StandardMaterial3D Unshaded(Color colour) => new StandardMaterial3D
        {
            AlbedoColor = colour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        // how many there are, and how many of those are already committed to the next throw
        public void Show(int nerve, int standing = 0)
        {
            if (_tokens == null) return;

            Showing = Mathf.Clamp(nerve, 0, Cap);
            Standing = Mathf.Clamp(standing, 0, Showing);

            for (int i = 0; i < _tokens.Length; i++)
            {
                bool held = i < Showing;

                _faces[i].MaterialOverride = held ? _full : _empty;
                _tokens[i].Position = Beside + new Vector3(
                    i * Pitch,
                    Lift + (held && i >= Showing - Standing ? Armed : 0f),
                    0f);
            }
        }

        // DEVELOPER ONLY - not localized, never reaches the screen
        public override string ToString() =>
            $"{Showing}/{Cap} nerve" + (Standing > 0 ? $", {Standing} committed" : "");
    }
}

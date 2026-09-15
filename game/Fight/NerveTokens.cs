using Godot;

namespace Game.Fight
{
    public partial class NerveTokens : Node3D
    {
        // a chip, not a pip: bigger because there are never more than five
        public const float Radius = 0.0042f;

        public const float Thickness = 0.0016f;

        public const float Pitch = 0.0105f;

        // same side as the vigor tally, below it, so both read as one column
        public static readonly Vector3 Beside = new Vector3(0.021f, 0f, -0.014f);

        public const float Lift = 0.0012f;

        // how far a committed token stands off the table
        public const float Armed = 0.010f;

        public Color Ink { get; set; } = new Color(0.86f, 0.72f, 0.36f);

        public Color Spent { get; set; } = new Color(0.34f, 0.31f, 0.26f, 0.30f);

        // its own body, so a click on a token is not read as a click on the square under it
        StaticBody3D[] _tokens;

        StandardMaterial3D _full;

        StandardMaterial3D _empty;

        public int Cap { get; private set; }

        public int Showing { get; private set; }

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

            // a shade wider than the chip, so a token is easy to hit at the table angle
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

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{Showing}/{Cap} nerve" + (Standing > 0 ? $", {Standing} committed" : "");
    }
}

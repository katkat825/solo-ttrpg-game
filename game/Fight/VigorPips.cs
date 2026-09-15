using Godot;

namespace Game.Fight
{
    public partial class VigorPips : Node3D
    {
        // 3 mm across on a 60 mm square
        public const float PipRadius = 0.0015f;

        public const float PipPitch = 0.0042f;

        public const int PerRow = 5;

        // clear of the 42 mm base, toward the near table edge
        public static readonly Vector3 Beside = new Vector3(0.021f, 0f, 0.020f);

        // a hair above the mat, so it reads as ink not an object
        public const float Lift = 0.0012f;

        public Color Ink { get; set; } = new Color(0.68f, 0.24f, 0.18f, 0.95f);

        public Color Spent { get; set; } = new Color(0.32f, 0.28f, 0.24f, 0.28f);

        MeshInstance3D[] _pips;

        StandardMaterial3D _full;

        StandardMaterial3D _empty;

        public int Max { get; private set; }

        public int Showing { get; private set; }

        // built once; rebuilding per hit would flicker the tally length
        public void Track(int maxVigor)
        {
            Max = maxVigor < 0 ? 0 : maxVigor;
            Showing = Max;

            Build();
        }

        void Build()
        {
            foreach (Node child in GetChildren()) child.QueueFree();

            _full = Unshaded(Ink);
            _empty = Unshaded(Spent);

            var dot = new SphereMesh
            {
                Radius = PipRadius,
                Height = PipRadius * 2f,
                RadialSegments = 8,
                Rings = 4,
            };

            _pips = new MeshInstance3D[Max];

            for (int i = 0; i < Max; i++)
            {
                var pip = new MeshInstance3D
                {
                    Name = $"Pip{i:00}",
                    Mesh = dot,
                    MaterialOverride = _full,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = Beside + new Vector3(
                        (i % PerRow) * PipPitch,
                        Lift,
                        (i / PerRow) * PipPitch),
                };

                _pips[i] = pip;
                AddChild(pip);
            }
        }

        static StandardMaterial3D Unshaded(Color colour) => new StandardMaterial3D
        {
            AlbedoColor = colour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        public void Show(int vigor)
        {
            if (_pips == null) return;

            Showing = Mathf.Clamp(vigor, 0, Max);

            for (int i = 0; i < _pips.Length; i++)
                _pips[i].MaterialOverride = i < Showing ? _full : _empty;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() => $"{Showing}/{Max} pips";
    }
}

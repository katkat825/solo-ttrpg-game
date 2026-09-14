using Godot;

namespace Game.Fight
{
    // VIGOR, ON THE TABLE (COMBAT_LOOP.md C0) - a tally beside the piece, not a bar in a corner.
    //
    // THE_TABLE.md 6 says the room is the entire UI, and a health bar is the single most reflexive
    // thing to reach for when a number has to be shown. What a table actually has is the DM's pad
    // with strokes on it, or a little pile of wound tokens next to the model, so this is strokes:
    // one pip per point of Vigor, in rows of five the way anybody tallies, lying on the mat beside
    // the piece. Damage puts pips out; it never moves them, because a tally you can count is a
    // tally whose length does not change while you are looking at it.
    //
    // A CHILD OF THE PIECE, so it goes where the piece goes and nothing has to remember to carry
    // it. That also settles what happens when a piece is lifted off the board: its tally goes with
    // it, because the tally was never a thing on the board - it was a thing beside the model.
    //
    // NO TEXT. Not because a numeral would be hard - Label3D exists - but because a number on the
    // mat is a readout, and CONVENTIONS.md's tiebreaker asks which reads more like a real table.
    // It also keeps this file out of the localization question entirely: there is nothing here to
    // translate, so there is nothing here to forget to translate.
    //
    // RABBLE GET NONE, and that is information rather than an omission: a piece with no tally
    // beside it is a piece with no health track, which is exactly what CORE_RULES.md section 8
    // says a Rabble is. Fight.Muster simply does not give them one.
    public partial class VigorPips : Node3D
    {
        // 3 mm across on a 60 mm square - a stroke on a pad, at the size a stroke would be
        public const float PipRadius = 0.0015f;

        public const float PipPitch = 0.0042f;

        // tallies are counted in fives everywhere anybody has ever kept one
        public const int PerRow = 5;

        // clear of the base (42 mm across) and toward the near edge of the table, where the DM's
        // hand would put a token rather than where it would hide the model
        public static readonly Vector3 Beside = new Vector3(0.021f, 0f, 0.020f);

        // a hair above the mat, for the same reason CellFlash is: ink on the map, not an object
        // lying on it
        public const float Lift = 0.0012f;

        // full and spent. warm ink and the ghost of it - the spent pips stay visible on purpose,
        // because "3 left of 20" and "3 left of 8" are different situations and a tally that
        // shortened as it emptied would show them the same way
        public Color Ink { get; set; } = new Color(0.68f, 0.24f, 0.18f, 0.95f);

        public Color Spent { get; set; } = new Color(0.32f, 0.28f, 0.24f, 0.28f);

        MeshInstance3D[] _pips;

        StandardMaterial3D _full;

        StandardMaterial3D _empty;

        public int Max { get; private set; }

        public int Showing { get; private set; }

        // how many points this piece has at full health. built once, because MaxVigor does not
        // change - a tally that was rebuilt on every hit would flicker its own length
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

            // one mesh shared by every pip - twenty spheres on the mat is twenty instances of one
            // small thing, which is what a MeshInstance is for
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

        // how much is left, right now. the pips beyond it go out
        public void Show(int vigor)
        {
            if (_pips == null) return;

            Showing = Mathf.Clamp(vigor, 0, Max);

            for (int i = 0; i < _pips.Length; i++)
                _pips[i].MaterialOverride = i < Showing ? _full : _empty;
        }

        // DEVELOPER ONLY - not localized, never reaches the screen
        public override string ToString() => $"{Showing}/{Max} pips";
    }
}

using System.Globalization;

namespace Content.Minis
{
    public readonly struct Tint
    {
        // a manifest that said nothing: wear the atlas as it came
        public static readonly Tint None = default;

        Tint(float r, float g, float b, bool something)
        {
            R = r;
            G = g;
            B = b;
            IsSomething = something;
        }

        // 0..1 as a shader wants them, converted once here
        public float R { get; }

        public float G { get; }

        public float B { get; }

        public bool IsSomething { get; }

        // #RRGGBB only; a mini has no alpha (that's the atlas's business)
        public static bool TryParse(string text, out Tint tint)
        {
            tint = None;

            if (string.IsNullOrWhiteSpace(text)) return false;

            string hex = text.Trim();

            if (hex.Length != 7 || hex[0] != '#') return false;

            if (!byte.TryParse(hex.Substring(1, 2), NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture, out byte r) ||
                !byte.TryParse(hex.Substring(3, 2), NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture, out byte g) ||
                !byte.TryParse(hex.Substring(5, 2), NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture, out byte b))
                return false;

            tint = new Tint(r / 255f, g / 255f, b / 255f, true);

            return true;
        }

        public override string ToString() =>
            IsSomething
                ? $"#{(int)(R * 255f + 0.5f):X2}{(int)(G * 255f + 0.5f):X2}{(int)(B * 255f + 0.5f):X2}"
                : "untinted";
    }
}

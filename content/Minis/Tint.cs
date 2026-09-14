using System.Globalization;

namespace Content.Minis
{
    // WHAT COLOUR TO PAINT IT, AS `#RRGGBB` AND NOTHING ELSE (MINIS_AND_ART.md A0).
    //
    // A0 asks for "a recolour (a palette swap or tint)". This is the tint, and the palette swap is
    // deliberately not here - for a reason that is a convention rather than a shortcut.
    //
    // WHY A TINT AND NOT A RUNTIME RE-BAKE. `tools/bake-palette.ps1` recolours a pack's ATLAS, a
    // texture at a time, offline. Doing that to a stranger's model at load would mean decoding,
    // re-mapping and re-uploading every texture in it while a player waits at a loading bar, for a
    // result the painted-miniature shader is about to relight anyway. The shader already has a
    // `tint` uniform that multiplies the atlas, and multiplying is what a wash over a base coat
    // physically is. So the treatment that moves to load time is the SHADER, which is the part
    // that makes nine packs read as one game; the bake stays where it is.
    //
    // WHY A PACK CANNOT WRITE `"tint": "bone"`. `CONVENTIONS.md` is explicit: "The palette is
    // stated once, in `tools/palette.ps1`. Not in a `.tres`, not in the shader, not in a doc." A
    // name here would need the fourteen colours in C# as well, and a palette written down twice is
    // one that drifts - with every asset in the game needing a re-bake to find out. If named
    // colours are ever wanted, the fix is to move the palette into one data file that the tools
    // and the game both read, and that is a change to the art pipeline rather than a field here.
    //
    // AND THE REST OF THE LOOK IS NOT AUTHORABLE AT ALL. `brush_strength`, `varnish`, `bands` and
    // the liner are the game's signature and not a pack's decision - a pack that could turn the
    // banding off would be a pack that stops looking like this game, which is the one thing the
    // treatment exists to prevent. Colour is the dial an author gets.
    public readonly struct Tint
    {
        // what a manifest that said nothing means: wear the atlas exactly as it came
        public static readonly Tint None = default;

        Tint(float r, float g, float b, bool something)
        {
            R = r;
            G = g;
            B = b;
            IsSomething = something;
        }

        // 0..1, the way a shader wants them, converted once here rather than at every use
        public float R { get; }

        public float G { get; }

        public float B { get; }

        public bool IsSomething { get; }

        // WHAT IT IS NOT: an alpha channel. A half-transparent miniature is not a thing a painter
        // can make, and the one place transparency matters on the table - a model whose atlas has
        // cut-out alpha in it - is the atlas's business and not the tint's
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

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            IsSomething
                ? $"#{(int)(R * 255f + 0.5f):X2}{(int)(G * 255f + 0.5f):X2}{(int)(B * 255f + 0.5f):X2}"
                : "untinted";
    }
}

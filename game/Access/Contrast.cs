using Godot;

namespace Game.Access
{
    // HOW HARD A THING IS TO READ, AS A NUMBER (AX3).
    //
    // Resizable text and a high-contrast mode are standing requirements. A high-contrast mode
    // built by eye is a mode that is high-contrast in the two places somebody looked, so the
    // standard's own measure is used instead: relative luminance and the contrast ratio out of
    // WCAG 2, which is the one figure there is for this and runs to black-on-white being 21:1.
    //
    // This table is lit warm and low on purpose and its paper is parchment,
    // so nothing here is going to reach 21 while still looking like a table - which is exactly why
    // the ratio is worth computing rather than asserting. It says what the ordinary table manages,
    // and high contrast is what it changes to when the ordinary table is not enough.
    //
    // Godot-free apart from Color, which is a struct, so the sweep in check-access.ps1 and the unit
    // tests measure the same numbers the running game does.
    public static class Contrast
    {
        // WCAG AA for body text
        public const float Body = 4.5f;

        // WCAG AA for large text, which is most of what is printed on a card at this scale
        public const float Large = 3f;

        // AAA, and what high contrast aims at
        public const float Plain = 7f;

        // the highest there is: black on white
        public const float Most = 21f;

        // one channel, out of sRGB and into light
        static float Linear(float channel) =>
            channel <= 0.03928f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);

        public static float Luminance(Color colour) =>
            0.2126f * Linear(colour.R) + 0.7152f * Linear(colour.G) + 0.0722f * Linear(colour.B);

        // ORDER DOES NOT MATTER, which is the point of doing it this way: ink on paper and paper
        // behind ink are the same legibility problem
        public static float Between(Color one, Color other)
        {
            float a = Luminance(one);
            float b = Luminance(other);

            return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
        }

        public static bool Enough(Color ink, Color paper, float ratio = Body) =>
            Between(ink, paper) >= ratio;

        // THE INK THIS PAPER CAN CARRY. Black or white, whichever wins - and it is genuinely one or
        // the other: anything in between is a compromise nobody asked for when they asked for high
        // contrast
        public static Color Ink(Color paper) =>
            Luminance(paper) > 0.18f ? Colors.Black : Colors.White;

        // and the outline that goes behind it, which is the other half of legibility on a 3D table:
        // text over a mat of unknown colour needs its own edge or it disappears into whatever is
        // behind it from this angle
        public static Color Edge(Color ink) =>
            Luminance(ink) > 0.18f ? Colors.Black : Colors.White;
    }
}

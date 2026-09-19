using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Access
{
    // YOUR OWN ARMS, AND WHOSE THEY LOOK LIKE (AX5).
    //
    // First-person hands on your side of the table are what make it a table
    // you are AT. Which means the arms are a picture of the player, and a picture of the player that
    // is wrong about them is worse than no picture: "it would be jarring for an african american to
    // see white arms, and the answer settled on is to do both
    // halves - a real range of human tones AND clearly non-human options, because green or furred
    // arms read as costume where the wrong skin tone reads as erasure.
    //
    // THE TONES ARE NAMED AS PIGMENTS, and that is deliberate. This is a game about painted
    // miniatures under a warm lamp; umber and sienna are what the paints are called, they carry no
    // claim about whose arm it is, and a locale can print whatever word its own language uses for the
    // colour. A numbered ladder would have been worse in every language.
    //
    // Pure. What the arms are MADE of is an art pass and needs a person; what
    // colour they are and whether they are there is a choice, and the choice is here.
    public enum Arm
    {
        Umber,

        Sienna,

        Almond,

        Wheat,

        Ivory,

        // the escape hatch, and the reason it is first-class: an arm that is obviously not anybody's
        // is an arm nobody has to see themselves in
        Goblin,

        Bear,
    }

    // TWO ARMS, ONE, OR NONE. Hidden is not a lesser option: a player using a screen reader has no
    // use for arms at all, and a player whose own hands are not what these arms are doing may
    // rather not be reminded
    public enum Shown
    {
        Both,

        Left,

        Right,

        Hidden,
    }

    public static class Arms
    {
        public static string Word(this Arm arm) => arm.ToString().ToLowerInvariant();

        public static string Word(this Shown shown) => shown.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words => Enum.GetValues<Arm>().Select(Word).ToArray();

        public static IReadOnlyList<string> Showings =>
            Enum.GetValues<Shown>().Select(Word).ToArray();

        // WHAT COLOUR, for the placeholder geometry that stands in until the models are made. The
        // colours are paint, so they are stated here beside the names rather than in a .tres nobody
        // would think to look in - and the fourteen-colour palette in tools/palette.ps1 stays the
        // authority for anything that ships as a model.
        public static Color Paint(this Arm arm) => arm switch
        {
            Arm.Umber => new Color("#4a2f1e"),
            Arm.Sienna => new Color("#77462a"),
            Arm.Almond => new Color("#a9733f"),
            Arm.Wheat => new Color("#cf9f6a"),
            Arm.Ivory => new Color("#e6c39a"),
            Arm.Goblin => new Color("#5c7a3a"),
            Arm.Bear => new Color("#3d2b20"),
            _ => new Color("#a9733f"),
        };

        public static bool IsHuman(this Arm arm) => arm is not (Arm.Goblin or Arm.Bear);

        // fur reads as fur by being rougher than skin, which is the whole of what the placeholder
        // can say about it
        public static float Rough(this Arm arm) => arm == Arm.Bear ? 1f : 0.62f;

        public static bool Left(this Shown shown) => shown is Shown.Both or Shown.Left;

        public static bool Right(this Shown shown) => shown is Shown.Both or Shown.Right;

        public static int Count(this Shown shown) =>
            (Left(shown) ? 1 : 0) + (Right(shown) ? 1 : 0);

        public static bool TryWord(string word, out Arm arm)
        {
            arm = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Arm one in Enum.GetValues<Arm>())
            {
                if (Word(one) != trimmed) continue;

                arm = one;
                return true;
            }

            return false;
        }

        public static bool TryWord(string word, out Shown shown)
        {
            shown = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Shown one in Enum.GetValues<Shown>())
            {
                if (Word(one) != trimmed) continue;

                shown = one;
                return true;
            }

            return false;
        }
    }
}

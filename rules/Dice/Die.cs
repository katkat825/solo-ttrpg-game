using System;

namespace Rules.Dice
{
    // the die sizes the game uses, and the ladder they step along
    // the enum value IS the side count, so a cast gives you the faces
    // None means no die at all - an untrained skill or an empty gear slot
    public enum Die
    {
        None = 0,
        D4 = 4,
        D6 = 6,
        D8 = 8,
        D10 = 10,
        D12 = 12
    }

    public static class DieExtensions
    {
        static readonly Die[] Ladder = { Die.D4, Die.D6, Die.D8, Die.D10, Die.D12 };

        public static int Sides(this Die d) => (int)d;

        public static bool IsReal(this Die d) => d != Die.None;

        // damage and strain step dice down - clamps at d4, never off the ladder
        public static Die StepDown(this Die d)
        {
            int i = Array.IndexOf(Ladder, d);
            if (i < 0) return Die.D4;
            return Ladder[Math.Max(0, i - 1)];
        }

        public static Die StepUp(this Die d)
        {
            int i = Array.IndexOf(Ladder, d);
            if (i < 0) return Die.D4;
            return Ladder[Math.Min(Ladder.Length - 1, i + 1)];
        }

        public static string Label(this Die d) => d == Die.None ? "-" : "d" + (int)d;
    }
}

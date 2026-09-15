using System;

namespace Core.Dice
{
    // the enum value is the side count, so a cast gives you the faces
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

        // one move along the ladder, not a replay, so the order modifiers arrived in doesn't matter
        public static Die StepBy(this Die d, int steps, out int delivered)
        {
            int i = Array.IndexOf(Ladder, d);

            if (i < 0)
            {
                delivered = 0;
                return d;
            }

            int landed = Math.Clamp(i + steps, 0, Ladder.Length - 1);
            delivered = landed - i;
            return Ladder[landed];
        }

        public static string Label(this Die d) => d == Die.None ? "-" : "d" + (int)d;
    }
}

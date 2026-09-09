using System;

namespace Core.Dice
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

        // moves a die n places along the ladder in one go - negative is down, positive is up
        // this is the only place a stack of modifiers reaches the ladder, and it is one move
        // rather than a replay, which is what makes the order effects arrived in irrelevant
        // (CORE_RULES.md section 9, "Saturation")
        //
        // <paramref name="delivered"/> is how far it actually moved, so a caller can tell three
        // steps down from d6 (a d4, two refused) from one step down from d6 (a d4, none refused)
        // clamping without reporting is the silent saturation this exists to remove
        //
        // None is not on the ladder at all: no die, nothing to step, delivered 0
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

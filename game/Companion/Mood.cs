using Content.Dialogue;
using Core.Resolution;

namespace Game.Companion
{
    // "It reacts to the table, not the fiction" (THE_TABLE.md section 4). A mood is not a feeling
    // about the story - it is what the creature is doing with its head while you throw.
    public enum Mood
    {
        // chin on the edge of the map, eyes half shut. Where it lives most of the time
        Calm,

        // the dice are in the air
        Watching,

        // something behind the screen, or a move it does not like
        Alert,

        // you are on the floor. THE_TABLE.md: "when you tip over, it goes quiet"
        Quiet,

        Pleased,
    }

    // the table's own events, read into the two things the companion has: a mood and a bark.
    // Godot-free, so what the creature reacts to is testable without a creature.
    public static class TableCues
    {
        // a Snag is exactly one 1 and is cosmetic; Trouble is two or more and is a real consequence
        public static Bark? For(PoolResult roll)
        {
            if (roll == null) return null;

            if (roll.Trouble) return Bark.Trouble;

            return roll.Snag ? Bark.Snag : (Bark?)null;
        }

        public static Mood MoodFor(Bark situation) => situation switch
        {
            Bark.Trouble => Mood.Alert,
            Bark.Secret => Mood.Alert,
            Bark.Nerve => Mood.Alert,
            Bark.Down => Mood.Quiet,
            Bark.Victory => Mood.Pleased,
            Bark.Camp => Mood.Calm,
            Bark.Overasked => Mood.Alert,
            _ => Mood.Watching,
        };

        // how long the creature holds a reaction before settling back to its idles
        public static double HoldFor(Mood mood) => mood switch
        {
            Mood.Quiet => 6.0,
            Mood.Alert => 2.4,
            Mood.Pleased => 2.0,
            Mood.Watching => 1.6,
            _ => 0.0,
        };

        // whether this mood overrides the one the creature is already in. Quiet outranks
        // everything: a hero on the floor is not something a good throw talks you out of.
        public static bool Outranks(Mood coming, Mood standing) =>
            standing != Mood.Quiet && Rank(coming) >= Rank(standing);

        static int Rank(Mood mood) => mood switch
        {
            Mood.Quiet => 4,
            Mood.Alert => 3,
            Mood.Pleased => 2,
            Mood.Watching => 1,
            _ => 0,
        };
    }
}

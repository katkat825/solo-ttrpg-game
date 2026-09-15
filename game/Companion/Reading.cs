using System;

namespace Game.Companion
{
    // How long a line stays up. Every word in this game is text (CONVENTIONS.md, "text before
    // voice"), so the thing a voiced game gets for free - a line that lasts as long as it takes to
    // say - has to be computed here, and getting it wrong is the difference between a companion
    // that talks to you and subtitles.
    //
    // Godot-free and by character count rather than word count, because a locale that does not put
    // spaces between words would otherwise flash its lines past.
    public static class Reading
    {
        // characters a second. Comfortable silent reading is nearer 20; this is deliberately slower,
        // because the player is also watching dice
        public const double Pace = 14.0;

        // nothing is ever up for less than this, however short: a two-word bark still has to register
        public const double Shortest = 1.6;

        // and nothing hangs there forever; a line this long is a line that wanted to be two
        public const double Longest = 9.0;

        // the beat before it goes, so the reader is not still finishing as it fades
        public const double Rest = 0.45;

        public static double Time(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return 0.0;

            return Math.Clamp(line.Length / Pace + Rest, Shortest, Longest);
        }

        // a line with a clip behind it lasts as long as the clip, floored the same way, so turning
        // voice on never makes text disappear early (W6)
        public static double Time(string line, double clipSeconds) =>
            clipSeconds <= 0.0
                ? Time(line)
                : Math.Max(Time(line), Math.Min(clipSeconds + Rest, Longest * 2.0));
    }
}

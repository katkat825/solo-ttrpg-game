using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Companions
{
    // Where the companion lives (THE_TABLE.md section 4). It sits on the TABLE, never on the map -
    // that is the conceit and it is also the mechanical guarantee: a creature with no square can
    // never be given a turn, a statblock or a pathfinder by some later well-meaning milestone.
    //
    // Closed, because each one is a place the engine knows how to find. A campaign choosing where
    // its companion sits is choosing from this list; a new perch is animation work, not data.
    public enum Perch
    {
        // Barbarian: a wolf lying alongside the map, chin on the edge
        MapEdge,

        // at the dice tray, where the dice come down: the Rogue's raven on the rim of it, and
        // since 2026-09-17 the house wolf on the table just behind it. A perch is a PLACE and no
        // class owns one - a campaign picks from this list for whatever creature it ships.
        TrayRim,

        // Mage: an imp on the closed rulebook, swinging its legs
        Rulebook,

        // Cleric: a reliquary standing upright, glow shifting with its mood
        Upright,

        // Druid: a small shape that never settles on one animal
        Unsettled,
    }

    public static class Perches
    {
        public static string Word(this Perch perch) => perch switch
        {
            Perch.MapEdge => "map_edge",
            Perch.TrayRim => "tray_rim",
            Perch.Rulebook => "rulebook",
            Perch.Upright => "upright",
            Perch.Unsettled => "unsettled",
            _ => perch.ToString().ToLowerInvariant(),
        };

        public static IReadOnlyList<string> Words => Enum.GetValues<Perch>().Select(Word).ToArray();

        public static bool TryWord(string word, out Perch perch)
        {
            perch = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Perch one in Enum.GetValues<Perch>())
            {
                if (Word(one) != trimmed) continue;

                perch = one;
                return true;
            }

            return false;
        }

        // the two that sit on another object, so the table has to find it before it can put them down
        public static bool NeedsTheTray(this Perch perch) => perch == Perch.TrayRim;

        public static bool NeedsTheBoard(this Perch perch) => perch == Perch.MapEdge;
    }
}

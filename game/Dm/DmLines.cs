using System.Collections.Generic;
using Core.Localization;

namespace Game.Dm
{
    // these live under combat.* (engine strings), not dialogue.dm.*, so campaigns don't have to translate them
    public static class DmLines
    {
        // the hit line: "best two, {0} - beats {1}. impact {2}"
        public static string Hit => KeyConventions.Key(KeyConventions.CombatNs, "throw", "hit");

        // the miss line: "best two, {0} - short of {1}", no impact
        public static string Miss => KeyConventions.Key(KeyConventions.CombatNs, "throw", "miss");

        // there was a third line here, saying a foe's Defence aloud the first time you beat it.
        // The number is printed on that foe's initiative card now, where it stays readable instead
        // of being repeated at you every swing
        public static IEnumerable<string> All()
        {
            yield return Hit;
            yield return Miss;
        }

        // both format numbers in, so a translation that dropped the placeholders would silently read as complete
        public static bool TakesAnArgument(string key) => key == Hit || key == Miss;
    }
}

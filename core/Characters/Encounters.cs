using System.Collections.Generic;

namespace Core.Characters
{
    // encounter shapes, built through IArchetypeSource and never from a concrete roster
    //
    // an encounter is content (ARCHITECTURE.md section 2) and will eventually be a file in a
    // campaign's encounters/ folder. this exists so that the sim and the tests can name the
    // standard fight without any of them reaching past the seam to do it - the ids below are
    // the contract, and any source that provides them works here, hard-coded or data-backed.
    //
    // when encounters become data this file is deleted rather than edited, and the callers
    // that ask a source for a fight keep asking a source for a fight
    public static class Encounters
    {
        // the standard first serious fight - N Rabble and one Rival
        // tuned against the sim: roughly 85% survival at ~6.6 rounds with the starting hero
        public static IList<Actor> Standard(this IArchetypeSource archetypes, int rabbleCount = 4)
        {
            var foes = new List<Actor>();

            // numbered from 1 so presentation can say "Rabble 3" without gluing a number
            // onto a translated name - see Actor.NameKey
            for (int i = 0; i < rabbleCount; i++)
                foes.Add(archetypes.Create(EngineIds.Rabble).Numbered(i + 1));

            foes.Add(archetypes.Create(EngineIds.Rival));

            return foes;
        }
    }

    // and the chapter boss, with whatever it is being fought alongside. Content in code exactly
    // as Standard is, and deleted the same day
    public static class BossFight
    {
        // one Dread and a handful of Rabble - a set piece rather than a duel, because a boss with
        // nothing else on the board is a boss the hero can walk away from and shoot at
        public static IList<Actor> WithRabble(this IArchetypeSource archetypes, int rabbleCount = 2)
        {
            var foes = new List<Actor>();

            for (int i = 0; i < rabbleCount; i++)
                foes.Add(archetypes.Create(EngineIds.Rabble).Numbered(i + 1));

            foes.Add(archetypes.Create(EngineIds.Dread));

            return foes;
        }
    }

    // the archetype ids the engine's own tests, sim and smoke scenes ask for by name.
    // they are ids and not display text, and they are stable - renaming one breaks every
    // locale file and every campaign that referenced it (KeyConventions rule 3).
    //
    // these sit here rather than on BuiltInArchetypes because a consumer naming a fighter
    // should not have to name the roster it came from. swapping the source is then a one-line
    // change at the composition root instead of an edit at every call site
    public static class EngineIds
    {
        public const string Barbarian = "barbarian";

        public const string Rabble = "rabble";

        public const string Rival = "rival";

        // A CASTER, so channelling can be seen working (COMBAT_LOOP.md C5). The Barbarian CAN
        // channel - untrained, two dice, no leftover, which is the rules working correctly and a
        // very poor way to look at a mechanic whose whole point is the leftover die. So the
        // placeholder roster grows one of these, and Board.HeroId is what picks him up
        public const string Mage = "mage";

        // and the boss tier, one per chapter (CORE_RULES.md section 8, COMBAT_LOOP.md C6)
        public const string Dread = "dread";
    }
}

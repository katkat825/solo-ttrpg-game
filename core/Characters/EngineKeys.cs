using System;
using System.Collections.Generic;
using Core.Localization;

namespace Core.Characters
{
    // every localization key the engine itself can put in front of a player
    // a missing string doesn't crash - it puts skill.larceny.name on screen and waits
    // so this is the checklist a locale file gets held to, in both directions
    // DERIVED, NEVER LISTED - hand-listing would be a second description free to drift
    // engine only, so it stops at the namespaces below - campaigns ship their own strings
    public static class EngineKeys
    {
        public static readonly IReadOnlyCollection<string> Namespaces = new[]
        {
            KeyConventions.ActorNs,
            KeyConventions.AttrNs,
            KeyConventions.SkillNs,
            KeyConventions.ConditionNs,
            KeyConventions.GearNs,
        };

        // order is stable and grouped by namespace
        // so a generated locale file diffs cleanly when something is added
        public static IEnumerable<string> All(IArchetypeSource archetypes = null)
        {
            foreach (Attr a in Enum.GetValues<Attr>())
            {
                yield return a.Key();
                yield return a.DescriptionKey();
            }

            foreach (Skill s in Enum.GetValues<Skill>())
            {
                yield return s.Key();
                yield return s.DescriptionKey();
            }

            foreach (Condition c in Enum.GetValues<Condition>())
            {
                yield return c.Key();
                yield return c.DescriptionKey();
            }

            foreach (Tier t in Enum.GetValues<Tier>())
                yield return t.Key();

            foreach (string key in ForRoster(archetypes ?? new BuiltInArchetypes()))
                yield return key;
        }

        // every archetype gets a numbered name as well as a plain one
        // only Rabble arrive in crowds today, but any foe can turn up twice
        // and "Rival 2" must come from one key with a {0} in it
        static IEnumerable<string> ForRoster(IArchetypeSource archetypes)
        {
            var gear = new SortedSet<string>();

            foreach (string id in archetypes.Ids)
            {
                yield return KeyConventions.ActorName(id);
                yield return KeyConventions.ActorNameNumbered(id);

                gear.Add(archetypes.Create(id).WeaponKey);
            }

            // Actor's own default, emitted whether or not an archetype uses it
            gear.Add(KeyConventions.GearName("unarmed"));

            foreach (string key in gear) yield return key;
        }

        // keys whose text must contain a {0}, because a caller formats a number in
        // a locale that drops the placeholder turns every mook into "Rabble"
        // and nothing anywhere reports a problem
        public static bool TakesAnArgument(string key) => key.EndsWith(".name_numbered", StringComparison.Ordinal);
    }
}

using System;
using System.Collections.Generic;
using Core.Localization;

namespace Core.Characters
{
    public static class EngineKeys
    {
        public static readonly IReadOnlyCollection<string> Namespaces = new[]
        {
            KeyConventions.ActorNs,
            KeyConventions.AttrNs,
            KeyConventions.SkillNs,
            KeyConventions.ConditionNs,
            KeyConventions.GearNs,

            KeyConventions.CombatNs,
        };

        // required, no default: a default would check the hardcoded roster while the real campaign shipped with no strings
        // throws here, not on first enumeration - the split-out iterator keeps a deferred throw from surfacing elsewhere
        public static IEnumerable<string> All(IArchetypeSource archetypes)
        {
            if (archetypes == null)
                throw new ArgumentNullException(
                    nameof(archetypes),
                    "EngineKeys.All needs the archetype source that is actually loaded. " +
                    "Defaulting to one would make the locale checklist describe a different game.");

            return Keys(archetypes);
        }

        static IEnumerable<string> Keys(IArchetypeSource archetypes)
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

            yield return KeyConventions.DefaultImpactName;

            yield return KeyConventions.GearName("unarmed");

            foreach (string key in ForRoster(archetypes))
                yield return key;
        }

        public static IEnumerable<string> ForRoster(IArchetypeSource archetypes)
        {
            var gear = new SortedSet<string>();

            foreach (string id in archetypes.Ids)
            {
                yield return KeyConventions.ActorName(id);
                yield return KeyConventions.ActorNameNumbered(id);

                gear.Add(archetypes.Create(id).WeaponKey);
            }

            foreach (string key in gear) yield return key;
        }

        // text must contain a {0} - a locale that drops it turns every mook into "Rabble", silently
        public static bool TakesAnArgument(string key) => key.EndsWith(".name_numbered", StringComparison.Ordinal);
    }
}

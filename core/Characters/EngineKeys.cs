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

            // the engine puts one combat key in front of a player: the name beside the default
            // Impact die when a pool left nothing over (KeyConventions.DefaultImpactName)
            KeyConventions.CombatNs,
        };

        // THE ROSTER IS REQUIRED, AND THAT IS THE POINT (F4)
        //
        // this took `IArchetypeSource archetypes = null` and fell back to `new BuiltInArchetypes()`.
        // it read as a convenience and it was a trap: when statblocks become campaign data, a
        // caller that forgot to say which roster is loaded would get the *hardcoded* three back,
        // check-locale.ps1 would pass, and the campaign that is actually loaded would ship with
        // no strings at all. a check that passes for the wrong reason is worse than no check, and
        // this was the last route by which the checklist could describe a different game
        //
        // of the two answers the milestone offered - throw, or omit the actor namespace - this is
        // the third and strictly better one: the parameter is required, so the *compiler* asks the
        // question and no caller can forget at runtime. omitting the actor keys would have failed
        // loudly too, but as "actor.rabble.name is in the file and nothing emits it", which sends
        // the reader to the locale file rather than to the caller that didn't name a roster
        //
        // null still throws, and it throws HERE rather than on first enumeration - the iterator is
        // split out below so a deferred exception can't surface somewhere unrelated
        //
        // order is stable and grouped by namespace
        // so a generated locale file diffs cleanly when something is added
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

            // Actor's own default weapon id, emitted whether or not an archetype uses it. It is
            // the ENGINE's - it is the string `Actor` falls back to - so it belongs here and not
            // in ForRoster, which is asked per roster and would otherwise demand it of every
            // campaign's locale for a weapon no campaign named (found by the P0 audit)
            yield return KeyConventions.GearName("unarmed");

            foreach (string key in ForRoster(archetypes))
                yield return key;
        }

        // WHAT ONE ROSTER'S ARCHETYPES NAME: their own names and the gear they are holding, and
        // nothing that belongs to the engine at large.
        //
        // every archetype gets a numbered name as well as a plain one
        // only Rabble arrive in crowds today, but any foe can turn up twice
        // and "Rival 2" must come from one key with a {0} in it
        //
        // PUBLIC SINCE P0, because a roster is no longer one thing. The engine ships its own and
        // every loaded campaign brings another, and their strings live in different files - the
        // engine's in game/locale/, a campaign's in its own folder (ARCHITECTURE.md section 4). So
        // the audit asks this per source rather than once, and `All` below is still the whole of
        // what the ENGINE names
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

        // keys whose text must contain a {0}, because a caller formats a number in
        // a locale that drops the placeholder turns every mook into "Rabble"
        // and nothing anywhere reports a problem
        public static bool TakesAnArgument(string key) => key.EndsWith(".name_numbered", StringComparison.Ordinal);
    }
}

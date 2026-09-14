using System;
using System.Collections.Generic;

namespace Core.Combat
{
    // THE NAMES A CAMPAIGN MAY CALL A BEHAVIOUR BY (ARCHITECTURE.md section 2).
    //
    // "The vocabulary is engine; which one fires when is data." A statblock says
    // `"behaviour": "strongest_first"` and gets `StrongestFirstSelector`. It cannot say
    // `"behaviour": "run a script"`, and that is not an oversight - content is pure data and never
    // code, because on a storefront that is a security boundary rather than a preference
    // (`CONTENT_PIPELINE.md`, `ARCHITECTURE.md` section 6).
    //
    // SO THE SEAM IS STILL OPEN AND THE DOOR IS STILL SHUT. `ITargetSelector` remains injectable
    // from code - `SeamTests` substitutes one from outside the library, `Encounter.Behaviour`
    // attaches one per foe, and C6's phase change swaps one at half Vigor. What a campaign gets is
    // the right to CHOOSE from the ones that ship, which is the whole of the difference between a
    // moddable game and an exploitable one.
    //
    // A CLOSED SET THAT GROWS IN ONE PLACE. Adding a selector means adding it here and writing its
    // class; nothing else changes, and a campaign naming one that does not exist is refused at
    // load with the list of the ones that do.
    public static class Behaviours
    {
        // clear the Rabble, then the weakest Rival. The engine's default and the sane one
        public const string RabbleFirst = "rabble_first";

        // always the toughest thing standing - for enemies that should be reckless
        public const string StrongestFirst = "strongest_first";

        static readonly Dictionary<string, Func<ITargetSelector>> Known =
            new Dictionary<string, Func<ITargetSelector>>(StringComparer.Ordinal)
            {
                [RabbleFirst] = () => RabbleFirstSelector.Instance,
                [StrongestFirst] = () => new StrongestFirstSelector(),
            };

        public static IReadOnlyCollection<string> All => Known.Keys;

        public static bool Has(string id) => id != null && Known.ContainsKey(id);

        // null for a name nobody knows, and null for no name at all - which means "the default",
        // and the default is the engine's rather than this class's opinion. `Encounter.BehaviourOf`
        // falls back to the engine's own selector, so a statblock that says nothing about
        // behaviour gets whatever the fight was built with
        public static ITargetSelector Of(string id) =>
            id != null && Known.TryGetValue(id, out Func<ITargetSelector> make) ? make() : null;
    }
}

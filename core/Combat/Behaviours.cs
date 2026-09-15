using System;
using System.Collections.Generic;

namespace Core.Combat
{
    // names only, never code - a campaign chooses a selector, it can't inject one (a security boundary)
    public static class Behaviours
    {
        public const string RabbleFirst = "rabble_first";

        public const string StrongestFirst = "strongest_first";

        static readonly Dictionary<string, Func<ITargetSelector>> Known =
            new Dictionary<string, Func<ITargetSelector>>(StringComparer.Ordinal)
            {
                [RabbleFirst] = () => RabbleFirstSelector.Instance,
                [StrongestFirst] = () => new StrongestFirstSelector(),
            };

        public static IReadOnlyCollection<string> All => Known.Keys;

        public static bool Has(string id) => id != null && Known.ContainsKey(id);

        public static ITargetSelector Of(string id) =>
            id != null && Known.TryGetValue(id, out Func<ITargetSelector> make) ? make() : null;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.World
{
    public sealed class Facts
    {
        readonly HashSet<string> _set = new HashSet<string>(StringComparer.Ordinal);

        Facts(string campaign)
        {
            Campaign = campaign;
        }

        public static Facts For(string campaign)
        {
            if (!Campaigns.ContentId.IsCampaign(campaign))
                throw new ArgumentException($"'{campaign}' is not a campaign id.", nameof(campaign));

            return new Facts(campaign);
        }

        public string Campaign { get; }

        public int Count => _set.Count;

        // true when this changed something; a trigger that fires on a fact wants to know
        public bool Set(string local) => _set.Add(Checked(local));

        public bool Clear(string local) => _set.Remove(Checked(local));

        public bool Is(string local) =>
            !string.IsNullOrEmpty(local) && _set.Contains(local);

        // in the author's own spelling; scoping is the store's identity, not the name's
        public IReadOnlyList<string> All =>
            _set.OrderBy(f => f, StringComparer.Ordinal).ToArray();

        public IEnumerable<string> Scoped =>
            All.Select(f => FactName.Scoped(Campaign, f));

        // reading a save back or folding sets together; unreadable names never get here
        public void Absorb(IEnumerable<string> locals)
        {
            foreach (string local in locals ?? Array.Empty<string>())
                if (FactName.IsLocal(local)) _set.Add(local);
        }

        static string Checked(string local)
        {
            if (!FactName.IsLocal(local))
                throw new ArgumentException(FactName.Explain(local), nameof(local));

            return local;
        }

        // debug only, never localized
        public override string ToString() =>
            Count == 0
                ? $"{Campaign}: nothing has happened yet"
                : $"{Campaign}: {Count} fact(s) - {string.Join(", ", All.Take(6))}" +
                  (Count > 6 ? ", ..." : "");
    }
}

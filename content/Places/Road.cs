using System;
using System.Collections.Generic;
using System.Linq;
using Content.World;
using Core.Dice;
using Core.Localization;

namespace Content.Places
{
    public sealed class Road
    {
        public Road(string id, string from, string to, IReadOnlyList<Happening> wayside)
        {
            Id = id;
            From = from ?? "";
            To = to ?? "";
            Wayside = wayside ?? Array.Empty<Happening>();
            Total = Wayside.Sum(h => h.Weight);
        }

        public string Id { get; }

        public string From { get; }

        public string To { get; }

        // in the author's order; the draw walks it, so the order is the file's
        public IReadOnlyList<Happening> Wayside { get; }

        public int Total { get; }

        public bool IsQuiet => Wayside.Count == 0 || Total <= 0;

        // a road is walked both ways; the two places name each other through it
        public bool Joins(string a, string b) =>
            (From == a && To == b) || (From == b && To == a);

        public string NameKey(string campaign) =>
            KeyConventions.Key(KeyConventions.QuestNs, campaign, Id, "name");

        // the road's name plus one line per event, each of which the locale audit holds the campaign to
        public IEnumerable<string> Keys(string campaign)
        {
            yield return NameKey(campaign);

            foreach (Happening happening in Wayside)
                if (happening.LineKey(campaign) is { } line) yield return line;
        }


        // null is a real answer and the common one: nothing interesting happened
        public Happening Draw(IRng rng, Facts facts)
        {
            if (rng == null || IsQuiet) return null;

            // a one-shot that has already fired is out of the table, not re-rolled inside it
            Happening[] left = Wayside
                .Where(h => h.CanHappen(facts))
                .ToArray();

            int total = left.Sum(h => h.Weight);

            if (total <= 0) return null;

            int rolled = rng.Roll(total);

            foreach (Happening happening in left)
            {
                rolled -= happening.Weight;

                if (rolled <= 0) return happening.IsNothing ? null : happening;
            }

            return null;
        }

        public override string ToString() =>
            $"{Id}: {From} <-> {To}, " +
            (IsQuiet ? "nothing on it" : $"{Wayside.Count} things that can happen");
    }
}

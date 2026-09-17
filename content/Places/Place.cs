using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.World;
using Core.Localization;

namespace Content.Places
{
    public sealed class Place
    {
        public Place(string id, string map, IReadOnlyList<Standing> standings,
                     IReadOnlyList<Exit> exits, IReadOnlyList<Trigger> triggers,
                     IReadOnlyList<Cue> cues)
        {
            Id = id;
            Map = map;
            Standings = standings ?? Array.Empty<Standing>();
            Exits = exits ?? Array.Empty<Exit>();
            Triggers = triggers ?? Array.Empty<Trigger>();
            Cues = cues ?? Array.Empty<Cue>();
        }

        public string Id { get; }

        // a file name in the campaign's maps/, without the extension
        public string Map { get; }

        public IReadOnlyList<Standing> Standings { get; }

        public IReadOnlyList<Exit> Exits { get; }

        public IReadOnlyList<Trigger> Triggers { get; }

        public IReadOnlyList<Cue> Cues { get; }

        // a place with a foe on it is somewhere a fight is waiting; one without is somewhere to be
        public bool IsAFight => Standings.Any(s => s.IsAFoe);

        public string NameKey(string campaign) =>
            KeyConventions.Key(KeyConventions.QuestNs, campaign, Id, "name");

        public IEnumerable<string> Keys(string campaign)
        {
            yield return NameKey(campaign);

            foreach (Cue cue in Cues)
                if (cue.LineKey(campaign) is { } line) yield return line;
        }

        // in the author's order; a different order tells a different story
        public IEnumerable<Cue> CuesFor(When moment)
        {
            foreach (Cue cue in Cues)
                if (cue.WhenIt == moment) yield return cue;
        }


        // base map plus facts applied, nothing else remembered; the corpse and scattered dice are transient
        public IEnumerable<Standing> On(Facts facts)
        {
            bool cleared = facts != null && facts.Is(FactName.Cleared(Id));

            foreach (Standing standing in Standings)
            {
                if (!standing.Needs.Met(facts)) continue;

                // a fight already won does not re-arm itself when you walk back through the room
                if (standing.IsAFoe && cleared) continue;

                if (!standing.IsAFoe && facts != null && facts.Is(FactName.Dead(standing.Entity)))
                    continue;

                yield return standing;
            }
        }

        public IEnumerable<Exit> Ways(Facts facts)
        {
            foreach (Exit exit in Exits)
                if (exit.Needs.Met(facts)) yield return exit;
        }

        // one id per slot, slot 1 first, with a hole where a slot is unused
        public IReadOnlyList<string> Roster(string campaign) => Roster(campaign, null);

        // the same roster as the facts leave it - what a fight here is actually against
        public IReadOnlyList<string> Roster(string campaign, Facts facts)
        {
            Standing[] foes = (facts == null ? Standings : On(facts)).Where(s => s.IsAFoe).ToArray();

            int last = 0;

            foreach (Standing standing in foes)
                if (standing.Slot > last) last = standing.Slot;

            var roster = new string[last];

            foreach (Standing standing in foes)
                roster[standing.Slot - 1] = campaign != null
                    ? ContentId.Scoped(campaign, standing.Monster)
                    : standing.Monster;

            return roster;
        }

        // debug only, never localized
        public override string ToString() =>
            $"{Id} on {Map}: {Standings.Count} standing, {Exits.Count} exits, " +
            $"{Triggers.Count} triggers, {Cues.Count} cues";
    }
}

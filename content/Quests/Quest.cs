using System.Collections.Generic;
using System.Linq;
using Content.World;
using Core.Localization;

namespace Content.Quests
{
    public sealed class Quest
    {
        public Quest(string id, Requirement offered, Requirement done, Requirement failed)
        {
            Id = id;
            Offered = offered ?? Requirement.Always;
            Done = done ?? Requirement.Always;
            Failed = failed;
        }

        public string Id { get; }

        // when the player may even know about it; Always for the ones a campaign opens with
        public Requirement Offered { get; }

        public Requirement Done { get; }

        // null where the author never allowed it to fail, which the validator holds them to
        public Requirement Failed { get; }

        public bool CanFail => Failed != null;

        // accepted is a fact like everything else; a conversation sets it and nothing special happens
        public string AcceptedFact => FactName.Accepted(Id);

        // failed is asked first: a quest with both conditions met has an impossible turn-in
        public QuestState StateIn(Facts facts)
        {
            if (!Offered.Met(facts)) return QuestState.Unknown;

            if (CanFail && Failed.Met(facts)) return QuestState.Failed;

            if (Done.Met(facts)) return QuestState.Done;

            return facts != null && facts.Is(AcceptedFact) ? QuestState.Active : QuestState.Offered;
        }

        public string TitleKey(string campaign) =>
            KeyConventions.Key(KeyConventions.QuestNs, campaign, Id, "title");

        public string DescriptionKey(string campaign) =>
            KeyConventions.Key(KeyConventions.QuestNs, campaign, Id, "description");

        public IEnumerable<string> Keys(string campaign)
        {
            yield return TitleKey(campaign);
            yield return DescriptionKey(campaign);
        }

        // every fact this quest reads, for the reachability pass
        public IEnumerable<string> Facts =>
            Offered.Facts
                   .Concat(Done.Facts)
                   .Concat(CanFail ? Failed.Facts : Enumerable.Empty<string>())
                   .Distinct(System.StringComparer.Ordinal);

        public override string ToString() =>
            $"{Id}: done {Done}" + (CanFail ? $", failed {Failed}" : ", and cannot fail") +
            (Offered.IsAlways ? "" : $", offered {Offered}");
    }
}

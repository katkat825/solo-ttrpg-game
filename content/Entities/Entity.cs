using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Items;
using Content.World;
using Core.Localization;

namespace Content.Entities
{
    public sealed class Entity
    {
        public Entity(string id, string mini, string monster,
                      IReadOnlyDictionary<Interaction, string> can, LootTable loot)
        {
            Id = id;
            Mini = mini ?? "";
            Monster = monster ?? "";
            Can = can ?? new Dictionary<Interaction, string>();
            Loot = loot ?? LootTable.Nothing;
        }

        // scoped by the campaign that read it, exactly as a monster id is
        public string Id { get; }

        public string Local => ContentId.LocalOf(Id);

        // empty means whatever the engine gives a nameless figure
        public string Mini { get; }

        // the statblock it fights as; empty unless the author permitted an attack
        public string Monster { get; }

        public bool Fights => Monster.Length > 0;

        // verb -> what the author wrote beside it; "" for verbs that need nothing said
        public IReadOnlyDictionary<Interaction, string> Can { get; }

        public LootTable Loot { get; }

        public bool Allows(Interaction verb) => Can.ContainsKey(verb);

        public string Named(Interaction verb) =>
            Can.TryGetValue(verb, out string name) ? name : "";

        public IEnumerable<Interaction> Verbs =>
            Enum.GetValues<Interaction>().Where(Allows);


        // the permitted set minus what the facts have already spent
        public IEnumerable<Interaction> Offers(Facts facts)
        {
            foreach (Interaction verb in Verbs)
            {
                if (verb.Repeatable()) { yield return verb; continue; }

                string spent = verb.Writes(Id is null ? "" : Local);

                if (facts != null && spent.Length > 0 && facts.Is(spent)) continue;

                yield return verb;
            }
        }

        public string NameKey => KeyConventions.ActorName(Id);

        // derived, never listed; a verb that reads a line out emits that line's key for the locale audit
        public IEnumerable<string> Keys()
        {
            yield return NameKey;

            string campaign = ContentId.CampaignOf(Id);

            if (campaign.Length == 0) yield break;

            foreach (KeyValuePair<Interaction, string> can in Can)
                if (can.Key == Interaction.Examine && can.Value.Length > 0)
                    yield return Places.Cue.Narration(campaign, can.Value);
        }

        public override string ToString() =>
            $"{Id}" +
            (Verbs.Any() ? $" - you may {string.Join(", ", Verbs.Select(v => v.Word()))}" : " - and you may do nothing with it") +
            (Fights ? $"; fights as {Monster}" : "") +
            (Mini.Length > 0 ? $"; stands as {Mini}" : "");
    }
}

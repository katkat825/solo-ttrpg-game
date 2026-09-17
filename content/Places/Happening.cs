using Content.World;

namespace Content.Places
{
    public sealed class Happening
    {
        public Happening(string id, int weight, string place = null, string sets = null,
                         bool once = false, Requirement needs = null)
        {
            Id = id ?? "";
            Weight = weight;
            Place = place ?? "";
            Sets = sets ?? "";
            Once = once;
            Needs = needs ?? Requirement.Always;
        }

        public string Id { get; }

        public int Weight { get; }

        // where this drops you; empty for something that happens without leaving the road
        public string Place { get; }

        public bool Stops => Place.Length > 0;

        public string Sets { get; }

        // a hand-authored one-shot rather than a stock event drawn over and over
        public bool Once { get; }

        public Requirement Needs { get; }

        // the empty entry: narrate and arrive, and it should be most of a good table
        public bool IsNothing => Id.Length == 0;

        public string HappenedFact => IsNothing ? "" : FactName.Happened(Id);

        public bool CanHappen(Facts facts)
        {
            if (Weight <= 0) return false;

            if (!Needs.Met(facts)) return false;

            return !Once || facts == null || !facts.Is(HappenedFact);
        }

        // the DM's line, the same key an examine or a cue resolves to
        public string LineKey(string campaign) =>
            IsNothing ? null : Cue.Narration(campaign, Id);

        public override string ToString() =>
            (IsNothing ? "nothing" : Id) + $" x{Weight}" +
            (Stops ? $" -> {Place}" : "") +
            (Sets.Length > 0 ? $", sets {Sets}" : "") +
            (Once ? ", once" : "");
    }
}

using Content.Dialogue;
using Core.Localization;

namespace Content.Places
{
    public sealed class Cue
    {
        public Cue(When when, string id, Gesture gesture = Gesture.Push, bool hesitant = false,
                   int slot = 0, string beat = null, string fact = null)
        {
            WhenIt = when;
            Id = id;
            Does = gesture;
            Hesitant = hesitant;
            Slot = slot;
            Beat = beat ?? "";
            Fact = fact ?? "";
        }

        public When WhenIt { get; }

        public string Id { get; }

        // push is the default; a cue that says nothing about itself is the DM delivering a line
        public Gesture Does { get; }

        public bool Hesitant { get; }

        // spawn slot for gestures on the map; 0 for the rest
        public int Slot { get; }

        public bool Tells => Does.Tells();

        public string Beat { get; }

        public bool Prompts => Beat.Length > 0;

        // the fact this cue waits on; empty unless WhenIt is When.Fact
        public string Fact { get; }

        // derived from the id, scoped to the campaign; DM narration is campaign content, not game/locale
        public string LineKey(string campaign) => !Tells ? null : Narration(campaign, Id);

        // the same key an entity's 'examine' resolves to, so one sentence has one spelling
        public static string Narration(string campaign, string id) =>
            KeyConventions.Key(KeyConventions.DialogueNs, "dm", "narration", campaign, id);

        public string BeatKey(string speaker, string campaign) =>
            !Prompts ? null : DialogueKeys.Beat(speaker, campaign, Beat);

        public override string ToString() =>
            $"{WhenIt.Word()}{(Fact.Length > 0 ? " " + Fact : "")}: {Id} - " +
            $"{(Hesitant ? "hesitant " : "")}{Does.Word()}" +
            (Slot > 0 ? $" at slot {Slot}" : "") +
            (Prompts ? $", prompts '{Beat}'" : "");
    }
}

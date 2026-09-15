using System;
using System.Collections.Generic;

namespace Content.Encounters
{
    public sealed class EncounterPlan
    {
        public EncounterPlan(string id, string map, IReadOnlyList<Placement> placements,
                             IReadOnlyList<Trigger> triggers, IReadOnlyList<Cue> cues)
        {
            Id = id;
            Map = map;
            Placements = placements ?? Array.Empty<Placement>();
            Triggers = triggers ?? Array.Empty<Trigger>();
            Cues = cues ?? Array.Empty<Cue>();
        }

        public string Id { get; }

        // a file name in the campaign's maps/, without the extension
        public string Map { get; }

        public IReadOnlyList<Placement> Placements { get; }

        public IReadOnlyList<Trigger> Triggers { get; }

        public IReadOnlyList<Cue> Cues { get; }

        // in the author's order; a different order tells a different story
        public IEnumerable<Cue> CuesFor(When moment)
        {
            foreach (Cue cue in Cues)
                if (cue.WhenIt == moment) yield return cue;
        }

        // one id per slot, slot 1 first, with a hole where a slot is unused; this is Fight.Spawns
        public IReadOnlyList<string> Roster(string campaign)
        {
            int last = 0;

            foreach (Placement placement in Placements)
                if (placement.Slot > last) last = placement.Slot;

            var roster = new string[last];

            foreach (Placement placement in Placements)
                roster[placement.Slot - 1] = campaign != null
                    ? Campaigns.ContentId.Scoped(campaign, placement.Monster)
                    : placement.Monster;

            return roster;
        }

        public override string ToString() =>
            $"{Id} on {Map}: {Placements.Count} placed, {Triggers.Count} triggers, {Cues.Count} cues";
    }

    public sealed class Placement
    {
        public Placement(int slot, string monster)
        {
            Slot = slot;
            Monster = monster;
        }

        // 1-9, matching the glyph drawn on the map
        public int Slot { get; }

        public string Monster { get; }

        public override string ToString() => $"{Monster} on spawn {Slot}";
    }

    // closed vocabulary; an open string here would be a scripting hook, and content is data, not code
    public sealed class Trigger
    {
        public Trigger(When when, Then then, string encounter)
        {
            WhenIt = when;
            ThenDo = then;
            Encounter = encounter ?? "";
        }

        public When WhenIt { get; }

        public Then ThenDo { get; }

        // empty for every verb but Goto
        public string Encounter { get; }

        public override string ToString() =>
            Encounter.Length > 0 ? $"{WhenIt} -> {ThenDo} {Encounter}" : $"{WhenIt} -> {ThenDo}";
    }

    public enum When
    {
        Entered,

        Cleared,
    }

    // one word each; Vocabulary lowercases the member name, so EndChapter would become "endchapter"
    public enum Then
    {
        Next,

        Goto,

        Ends,
    }

    public sealed class Cue
    {
        public Cue(When when, string id, Gesture gesture = Gesture.Push, bool hesitant = false,
                   int slot = 0, string beat = null)
        {
            WhenIt = when;
            Id = id;
            Does = gesture;
            Hesitant = hesitant;
            Slot = slot;
            Beat = beat ?? "";
        }

        public When WhenIt { get; }

        public string Id { get; }

        // Push is the default: a cue that says nothing about itself is the DM delivering a line
        public Gesture Does { get; }

        public bool Hesitant { get; }

        // spawn slot for gestures that happen on the map; 0 for the rest
        public int Slot { get; }

        public bool Tells => Does.Tells();

        // a beat of the shared spine the companion answers this moment with (W5); empty for most cues
        public string Beat { get; }

        public bool Prompts => Beat.Length > 0;

        // derived from the id, scoped to the campaign; DM narration is campaign content, never game/locale
        public string LineKey(string campaign) =>
            !Tells ? null
                   : Core.Localization.KeyConventions.Key(
                         Core.Localization.KeyConventions.DialogueNs, "dm", "narration",
                         campaign, Id);

        // whichever companion is at this table says it; the beat exists once and every voice has one
        public string BeatKey(string speaker, string campaign) =>
            !Prompts ? null : Dialogue.DialogueKeys.Beat(speaker, campaign, Beat);

        public override string ToString() =>
            $"{WhenIt}: {Id} - {(Hesitant ? "hesitant " : "")}{Does.Word()}" +
            (Slot > 0 ? $" at slot {Slot}" : "") +
            (Prompts ? $", prompts '{Beat}'" : "");
    }
}

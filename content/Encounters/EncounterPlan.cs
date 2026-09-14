using System;
using System.Collections.Generic;

namespace Content.Encounters
{
    // ONE ROOM, ONE FIGHT, WRITTEN DOWN (CONTENT_PIPELINE.md P4).
    //
    //     {
    //       "id": "ash_yard",
    //       "map": "ash_yard",
    //       "placements": [
    //         { "slot": 1, "monster": "cinder_hound" },
    //         { "slot": 2, "monster": "ghoul" }
    //       ],
    //       "triggers": [ { "when": "cleared", "then": "next" } ],
    //       "cues":     [ { "when": "entered", "cue": "the_yard_opens" } ]
    //     }
    //
    // THE MAP SAYS WHERE AND THIS SAYS WHO - the split P2 built and the reason it built it. A map
    // draws spawn slots `1`-`9` and never says what stands on them, so the same yard is four
    // hounds in one chapter and a ghoul in the next, and neither chapter redraws it.
    //
    // `EncounterPlan` AND NOT `Encounter`, because `Core.Combat.Encounter` is the fight actually
    // being played and this is the note it was set up from. Two types with one name, one of them
    // rules and the other content, is a `using` alias in every file that touches both.
    //
    // A CAMPAIGN PLACES ITS OWN MONSTERS AND NOBODY ELSE'S. `monster` is a local id - `ghoul`, not
    // `ashfall.ghoul` - and the loader scopes it to the campaign it was read from. That is what
    // makes a folder self-contained (ARCHITECTURE.md section 4): move it to another machine with
    // no other campaigns installed and it still plays. A campaign that wants a rabble writes one.
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

        // the name of a file in the campaign's own maps/, without the extension
        public string Map { get; }

        public IReadOnlyList<Placement> Placements { get; }

        public IReadOnlyList<Trigger> Triggers { get; }

        public IReadOnlyList<Cue> Cues { get; }

        // WHO STANDS WHERE, as the fight wants it: one id per slot, slot 1 first, with a hole
        // where the encounter uses no such slot. The scene export `Fight.Spawns` is exactly this
        // list, which is the whole of what "moving it into a campaign" cost
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

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{Id} on {Map}: {Placements.Count} placed, {Triggers.Count} triggers, {Cues.Count} cues";
    }

    // ONE FOE ON ONE OF THE MAP'S SPAWN SLOTS
    public sealed class Placement
    {
        public Placement(int slot, string monster)
        {
            Slot = slot;
            Monster = monster;
        }

        // 1-9, matching the glyph drawn on the map (MapReader.FirstSpawnGlyph)
        public int Slot { get; }

        // a local id inside this campaign's monsters/
        public string Monster { get; }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() => $"{Monster} on spawn {Slot}";
    }

    // WHAT HAPPENS WHEN. A closed vocabulary on both halves, exactly like `Tile` and `Edge`, for
    // the same reason those are closed: what a trigger DOES is a rule, and the file that says
    // which one fires is data. An open string here would be a scripting hook, and
    // `ARCHITECTURE.md` section 9 is explicit that content is pure data and never code - on a
    // storefront that is a security boundary rather than a preference.
    //
    // NOTHING RUNS THESE YET, and the milestone record says so plainly. What exists now is the
    // schema, the validation and the cross-reference check: a trigger naming an encounter that is
    // not in the chapter is refused at load. The runner that acts on them is Phase R's shell,
    // which is where "what happens after a fight" belongs - and a campaign authored today will
    // not be re-authored to gain it, which is the reason the slot is cut now.
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

        // which encounter `Then.Goto` goes to, and empty for every other verb
        public string Encounter { get; }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            Encounter.Length > 0 ? $"{WhenIt} -> {ThenDo} {Encounter}" : $"{WhenIt} -> {ThenDo}";
    }

    public enum When
    {
        // the fight begins
        Entered,

        // every foe is down
        Cleared,
    }

    // ONE WORD EACH, because `Vocabulary` derives the authorable word from the enum member by
    // lowercasing it - so a member called `EndChapter` would have to be written "endchapter",
    // which is a schema keeping a secret about how it is spelled
    public enum Then
    {
        // on to the next encounter in this chapter
        Next,

        // on to a named one, which is how a campaign branches
        Goto,

        // the chapter is over
        Ends,
    }

    // A SLOT FOR A GESTURE THE DM HAS NOT LEARNED YET (Phase D).
    //
    // `CONTENT_PIPELINE.md` P4 is explicit that this is a placeholder - "the cues are placeholders
    // until Phase D, but the data slot exists now so a campaign never has to be re-authored to
    // gain them". So the id is carried and validated as an id, and NO key is derived from it: what
    // a cue turns into - a gesture, a line, a look up from behind the screen - is Phase D's to
    // decide, and deriving a key here would be this file guessing at that shape and being wrong.
    public sealed class Cue
    {
        public Cue(When when, string id)
        {
            WhenIt = when;
            Id = id;
        }

        public When WhenIt { get; }

        public string Id { get; }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() => $"{WhenIt}: {Id}";
    }
}

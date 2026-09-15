using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Classes;
using Content.Encounters;
using Content.Kits;
using Content.Items;
using Content.Minis;
using Content.Monsters;
using Content.Schema;
using Core.Characters;
using Core.Localization;
using Core.Space;

namespace Game.Campaigns
{
    public sealed class Loaded
    {
        public Loaded(Package package, int strings, IReadOnlyList<string> missing = null)
        {
            Package = package;
            Strings = strings;
            Missing = missing ?? System.Array.Empty<string>();
        }

        public Package Package { get; }

        public string Id => Package.Id;

        public string Folder => Package.Folder;

        public Manifest Manifest => Package.Manifest;

        public JsonArchetypeSource Monsters => Package.Monsters;

        public ClassRoster Classes => Package.Classes;

        public KitBook Kit => Package.Kit;

        public ItemCatalogue Items => Package.Items;

        public Content.Dialogue.DialogueBook Dialogue => Package.Dialogue;

        public Content.Dialogue.BarkBook Barks => Package.Barks;

        public Content.Dialogue.BeatBook Beats => Package.Beats;

        public Content.Dialogue.HintBook Hints => Package.Hints;

        public Content.Companions.CompanionBook Companions => Package.Companions;

        public Content.Sheet.SheetOptions Sheet => Package.Sheet;

        public EncounterBook Encounters => Package.Encounters;

        public IReadOnlyDictionary<string, MapLayout> Maps => Package.Maps;

        public IReadOnlyList<ContentProblem> Problems => Package.Problems;

        public bool Failed => Package.Failed;

        public IReadOnlyList<string> Missing { get; }

        public bool Waiting => !Failed && Missing.Count > 0;

        public int Strings { get; }

        // voices: every creature the whole shelf can speak as, because a beat must be deliverable
        // by any companion the player might have. Null falls back to this pack's own, which is what
        // a single-pack test wants and is never what the game wants.
        public IEnumerable<string> Keys(IEnumerable<string> voices = null)
        {
            if (Failed || Waiting) yield break;

            foreach (string key in Manifest.Keys()) yield return key;

            foreach (string key in EngineKeys.ForRoster(Monsters)) yield return key;

            foreach (string id in Items.Ids.OrderBy(i => i, System.StringComparer.Ordinal))
                yield return KeyConventions.GearName(id);

            foreach (ClassCard card in Classes.All)
                foreach (string key in card.Keys())
                    yield return key;

            foreach (Content.Encounters.EncounterPlan plan in Encounters.All)
                foreach (Content.Encounters.Cue cue in plan.Cues)
                    if (cue.LineKey(Id) is { } line)
                        yield return line;

            foreach (Ability ability in Kit.All)
                foreach (string key in ability.Keys())
                    yield return key;

            foreach (MiniManifest mini in Package.Minis.All) yield return mini.NameKey;

            // Phase W. Every word a voice at this table can be asked for: the conversations, the
            // bark banks, and one phrasing of every beat for every voice - which is the shared
            // spine's whole claim, checked rather than promised (W5).
            foreach (string key in Dialogue.Keys()) yield return key;

            foreach (string key in Barks.Keys()) yield return key;

            // Phase R. The names on the blanks of a character sheet
            foreach (string key in Sheet.Keys()) yield return key;
        }

        // Keys a campaign MAY carry without being asked for them, and which are not orphans when
        // it does. The shared spine is the whole of this category: a beat has a phrasing per voice
        // and one for the DM, and a campaign is only ever expected to write SOME of them - it
        // cannot write for a companion published after it. Which ones it must write is a question
        // about delivery rather than about coverage, and check-dialogue.ps1 is where it is asked.
        public IEnumerable<string> Spare(IEnumerable<string> voices = null)
        {
            if (Failed || Waiting) yield break;

            foreach (string key in Beats.KeysFor(voices ?? Companions.Voices)) yield return key;
        }

        public override string ToString() =>
            Failed ? Package.ToString()
          : Waiting ? $"{Package} - NOT LOADED, it needs {string.Join(", ", Missing)}"
          : $"{Package}, {Strings} strings";
    }
}

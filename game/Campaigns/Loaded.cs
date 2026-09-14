using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Encounters;
using Content.Items;
using Content.Minis;
using Content.Monsters;
using Content.Schema;
using Core.Characters;
using Core.Localization;
using Core.Space;

namespace Game.Campaigns
{
    // ONE CAMPAIGN, AS IT CAME OFF THE DISK (CONTENT_PIPELINE.md P0-P4).
    //
    // A campaign is a folder, `Content.Campaigns.Package` is what that folder turned into, and
    // this is the same thing with the one part that needs Godot attached to it: its strings, which
    // have to reach a `TranslationServer`. Everything else here forwards, deliberately - a wrapper
    // that re-states its contents is two descriptions of one campaign, free to drift.
    //
    // `Library` folds several of these together so the game sees one roster and one catalogue. The
    // folds are lossy on purpose in two places, and both need a campaign to still know what it
    // brought: the locale audit asks "does ashfall ship a name for everything ashfall names", and
    // the shelf asks "which of these failed, and why".
    public sealed class Loaded
    {
        public Loaded(Package package, int strings, IReadOnlyList<string> missing = null)
        {
            Package = package;
            Strings = strings;
            Missing = missing ?? System.Array.Empty<string>();
        }

        public Package Package { get; }

        // the campaign's unique id, declared in its `campaign.json` and matching its folder name
        public string Id => Package.Id;

        public string Folder => Package.Folder;

        // null when the folder failed to load, which is the state the shelf has to be able to show
        public Manifest Manifest => Package.Manifest;

        public JsonArchetypeSource Monsters => Package.Monsters;

        public ItemCatalogue Items => Package.Items;

        public EncounterBook Encounters => Package.Encounters;

        public IReadOnlyDictionary<string, MapLayout> Maps => Package.Maps;

        public IReadOnlyList<ContentProblem> Problems => Package.Problems;

        // NOTHING OF A FAILED CAMPAIGN IS IN PLAY - not its monsters, not its items, not its
        // strings. It is on the shelf and it is on the shelf as broken (the isolation boundary,
        // ARCHITECTURE.md section 9)
        public bool Failed => Package.Failed;

        // THE PACKS IT NEEDED AND THE SHELF HAS NOT GOT (MINIS_AND_ART.md A4). A campaign here is
        // not broken - it is waiting for a subscription - and the difference matters to the person
        // reading the shelf: one of them is a bug report and the other is a button to press
        public IReadOnlyList<string> Missing { get; }

        // NOTHING OF IT IS IN PLAY EITHER WAY. A campaign loaded with a box where every monster
        // should be looks like a broken game rather than a missing pack, so a missing dependency
        // is all-or-nothing exactly as a failed read is
        public bool Waiting => !Failed && Missing.Count > 0;

        // how many locale strings it brought
        public int Strings { get; }

        // EVERY KEY THIS CAMPAIGN PUTS IN FRONT OF A PLAYER, derived and never listed - the same
        // rule `EngineKeys` follows, applied to a folder. The audit holds the campaign's own
        // `locale/` to exactly this, in both directions
        public IEnumerable<string> Keys()
        {
            if (Failed || Waiting) yield break;

            // ITS OWN NAME FIRST (P4). A campaign's title is not a field in `campaign.json` - it
            // is a key derived from the id, because a title in a data file is a string that cannot
            // be translated. So the shelf reads this, and the audit demands it
            foreach (string key in Manifest.Keys()) yield return key;

            foreach (string key in EngineKeys.ForRoster(Monsters)) yield return key;

            // and the gear lying around in it, which no statblock is holding
            foreach (string id in Items.Ids.OrderBy(i => i, System.StringComparer.Ordinal))
                yield return KeyConventions.GearName(id);

            // AND WHAT ITS FIGURES ARE CALLED (Phase A). A mini's display name is derived from its
            // id and never written in the manifest (`MiniManifest`), so this is the whole of what
            // a pack's `minis/` folder demands of its locale - and the audit holds the folder to
            // it in both directions, exactly as it does the monsters
            foreach (MiniManifest mini in Package.Minis.All) yield return mini.NameKey;
        }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            Failed ? Package.ToString()
          : Waiting ? $"{Package} - NOT LOADED, it needs {string.Join(", ", Missing)}"
          : $"{Package}, {Strings} strings";
    }
}

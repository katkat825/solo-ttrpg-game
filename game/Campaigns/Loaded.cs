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

        public EncounterBook Encounters => Package.Encounters;

        public IReadOnlyDictionary<string, MapLayout> Maps => Package.Maps;

        public IReadOnlyList<ContentProblem> Problems => Package.Problems;

        public bool Failed => Package.Failed;

        public IReadOnlyList<string> Missing { get; }

        public bool Waiting => !Failed && Missing.Count > 0;

        public int Strings { get; }

        public IEnumerable<string> Keys()
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
        }

        public override string ToString() =>
            Failed ? Package.ToString()
          : Waiting ? $"{Package} - NOT LOADED, it needs {string.Join(", ", Missing)}"
          : $"{Package}, {Strings} strings";
    }
}

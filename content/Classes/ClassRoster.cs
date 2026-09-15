using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Items;
using Content.Schema;
using Core.Characters;

namespace Content.Classes
{
    public sealed class ClassRoster : IArchetypeSource, IPackRoster
    {
        public const string Extension = ".json";

        readonly Dictionary<string, ClassCard> _classes =
            new Dictionary<string, ClassCard>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        ItemCatalogue _items;

        ClassRoster(string pack) => Pack = pack ?? "";

        // empty for an unscoped roster
        public string Pack { get; }

        public IReadOnlyCollection<string> Ids => _classes.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _classes.ContainsKey(id);

        public IEnumerable<ClassCard> All =>
            _classes.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => c.Value);

        public ClassCard Of(string id) =>
            id != null && _classes.TryGetValue(id, out ClassCard card) ? card : null;

        // always a new instance; actors are mutable and a shared hero would carry the last fight's wounds
        public Actor Create(string id) =>
            _classes.TryGetValue(id, out ClassCard card)
                ? card.Create(_items)
                : throw new KeyNotFoundException($"No archetype '{id}'.");

        // set by Package after items/ is read; left unset, a hero is created empty-handed
        public void Equips(ItemCatalogue items) => _items = items;


        // a missing folder is fine; a classes/ that exists and holds something broken is not
        public static ClassRoster Read(string folder, string pack)
        {
            var roster = new ClassRoster(pack);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return roster;

            foreach (string path in Files(folder))
            {
                string name = Path.GetFileName(path);
                string text;

                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception could)
                {
                    roster._problems.Add(new ContentProblem(name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<ClassCard> read = ClassReader.Parse(text, name, pack);

                if (!read.Ok)
                {
                    roster._problems.AddRange(read.Problems);
                    continue;
                }

                if (roster._classes.ContainsKey(read.Value.Id))
                {
                    roster._problems.Add(new ContentProblem(
                        name, "id",
                        $"'{read.Value.Id}' is already the id of another class in this folder"));
                    continue;
                }

                roster._classes[read.Value.Id] = read.Value;
            }

            return roster;
        }


        public static ClassRoster Of(string pack, params ClassCard[] cards)
        {
            var roster = new ClassRoster(pack);

            foreach (ClassCard card in cards ?? Array.Empty<ClassCard>())
                if (card?.Id != null) roster._classes[card.Id] = card;

            return roster;
        }

        // sorted; a load order that changes between machines is a bug nobody can reproduce
        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        public override string ToString() =>
            $"{_classes.Count} classes" +
            (Pack.Length > 0 ? $" from {Pack}" : "") +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}

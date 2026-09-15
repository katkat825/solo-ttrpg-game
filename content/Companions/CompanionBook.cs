using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Companions
{
    // a pack's companions/ folder, one file per creature
    public sealed class CompanionBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, CompanionCard> _companions =
            new Dictionary<string, CompanionCard>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        CompanionBook(string pack) => Pack = pack ?? "";

        public string Pack { get; }

        public IReadOnlyCollection<string> Ids => _companions.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _companions.ContainsKey(id);

        public CompanionCard Of(string id) =>
            id != null && _companions.TryGetValue(id, out CompanionCard card) ? card : null;

        public IEnumerable<CompanionCard> All =>
            _companions.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _companions[i]);

        // the creatures this pack can speak as; the spine and the locale audit both ask for this
        public IEnumerable<string> Voices =>
            All.Select(c => c.Voice).Distinct().OrderBy(v => v, StringComparer.Ordinal);

        public int Count => _companions.Count;


        public static CompanionBook Read(string folder, string pack)
        {
            var book = new CompanionBook(pack);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return book;

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
                    book._problems.Add(new ContentProblem(name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<CompanionCard> read = CompanionReader.Parse(text, name, pack);

                if (!read.Ok)
                {
                    book._problems.AddRange(read.Problems);
                    continue;
                }

                book.Add(read.Value, name);
            }

            return book;
        }

        void Add(CompanionCard card, string file)
        {
            if (_companions.ContainsKey(card.Id))
            {
                _problems.Add(new ContentProblem(
                    file, "id", $"'{card.Id}' is already a companion in this pack"));
                return;
            }

            _companions[card.Id] = card;
        }

        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        public override string ToString() =>
            $"{_companions.Count} companions" +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}

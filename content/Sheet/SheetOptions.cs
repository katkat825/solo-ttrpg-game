using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Sheet
{
    // What a pack offers the blanks on a sheet: sheet/races/ and sheet/backgrounds/.
    //
    // "Fill in race, class, background, appearance, with dropdowns where a real sheet has a blank"
    // (ROOM_AND_SHEET.md R0). The class comes out of classes/ and is the one blank with mechanics
    // behind it; these two are the rest of the line, and they are content for the same reason a
    // class is - adding a race must never be a code change.
    public sealed class SheetOptions
    {
        public const string Extension = ".json";

        readonly Dictionary<string, TraitCard> _cards =
            new Dictionary<string, TraitCard>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        SheetOptions(string pack) => Pack = pack ?? "";

        public string Pack { get; }

        public IReadOnlyCollection<string> Ids => _cards.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _cards.ContainsKey(id);

        public TraitCard Of(string id) =>
            id != null && _cards.TryGetValue(id, out TraitCard card) ? card : null;

        public IEnumerable<TraitCard> All =>
            _cards.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _cards[i]);

        public IEnumerable<TraitCard> For(Blank blank) => All.Where(c => c.Fills == blank);

        public int Count => _cards.Count;

        public IEnumerable<string> Keys() => All.SelectMany(c => c.Keys());


        public static SheetOptions Read(string folder, string pack)
        {
            var options = new SheetOptions(pack);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return options;

            foreach (Blank blank in Enum.GetValues<Blank>())
                options.ReadAll(Path.Combine(folder, blank.Folder()), blank, pack);

            return options;
        }

        void ReadAll(string folder, Blank blank, string pack)
        {
            if (!Directory.Exists(folder)) return;

            foreach (string path in Files(folder))
            {
                string name = blank.Folder() + "/" + Path.GetFileName(path);
                string text;

                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception could)
                {
                    _problems.Add(new ContentProblem(name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<TraitCard> read = SheetReader.Parse(text, name, pack, blank);

                if (!read.Ok)
                {
                    _problems.AddRange(read.Problems);
                    continue;
                }

                Add(read.Value, name);
            }
        }

        void Add(TraitCard card, string file)
        {
            if (!_cards.ContainsKey(card.Id))
            {
                _cards[card.Id] = card;
                return;
            }

            // a race and a background sharing an id is not a near miss: both are keyed under
            // class.<pack>.<id>.name and one of their names would silently never be seen
            _problems.Add(new ContentProblem(
                file, "id",
                $"'{ContentId.LocalOf(card.Id)}' is already a " +
                $"{_cards[card.Id].Fills.Word()} in this pack, and both would be named " +
                $"'{card.NameKey}' - one of the two would never be seen"));
        }

        // the same collision, across the folder boundary: a class and a race share the namespace
        public void MustNotCollideWith(Content.Classes.ClassRoster classes,
                                       List<ContentProblem> problems)
        {
            if (classes == null || problems == null) return;

            foreach (TraitCard card in All)
            {
                if (!classes.Has(card.Id)) continue;

                problems.Add(new ContentProblem(
                    card.Fills.Folder() + "/" + ContentId.LocalOf(card.Id) + Extension, "id",
                    $"'{ContentId.LocalOf(card.Id)}' is both a {card.Fills.Word()} and a class in " +
                    $"this pack, and both are named '{card.NameKey}' - rename one"));
            }
        }

        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        public override string ToString() =>
            _cards.Count == 0
                ? "no sheet options"
                : string.Join(", ", Enum.GetValues<Blank>()
                                        .Select(b => $"{For(b).Count()} {b.Folder()}")) +
                  (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;

namespace Content.Places
{
    public sealed class PlaceBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Place> _places =
            new Dictionary<string, Place>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        public IReadOnlyCollection<string> Ids => _places.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _places.ContainsKey(id);

        // null rather than throw; a save may name a place from a campaign nobody installed
        public Place Of(string id) =>
            id != null && _places.TryGetValue(id, out Place place) ? place : null;

        public IEnumerable<Place> All =>
            _places.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _places[i]);

        public int Count => _places.Count;


        public static PlaceBook Read(string folder)
        {
            var book = new PlaceBook();

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
                    book._problems.Add(new ContentProblem(
                        name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<Place> read = PlaceReader.Parse(text, name);

                if (!read.Ok)
                {
                    book._problems.AddRange(read.Problems);
                    continue;
                }

                book.Add(read.Value, name);
            }

            return book;
        }

        void Add(Place place, string file)
        {
            if (_places.ContainsKey(place.Id))
            {
                _problems.Add(new ContentProblem(
                    file, "id",
                    $"'{place.Id}' is already the id of another place in this folder"));
                return;
            }

            _places[place.Id] = place;
        }

        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        public override string ToString() =>
            $"{_places.Count} places" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}

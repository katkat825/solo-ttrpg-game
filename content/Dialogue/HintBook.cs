using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;

namespace Content.Dialogue
{
    // a campaign's hints/ folder: one problem, three rungs, each rung naming a BEAT rather than a
    // line. That indirection is the point - a hint phrased by only one companion is a hint gated on
    // which class you picked, and the spine check catches that the moment a voice is missing one.
    public sealed class HintBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Hint> _hints = new Dictionary<string, Hint>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        HintBook(string campaign) => Campaign = campaign ?? "";

        public string Campaign { get; }

        public IReadOnlyCollection<string> Ids => _hints.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _hints.ContainsKey(id);

        public Hint Of(string id) => id != null && _hints.TryGetValue(id, out Hint hint) ? hint : null;

        public IEnumerable<Hint> All =>
            _hints.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _hints[i]);

        public int Count => _hints.Count;


        public static HintBook Read(string folder, string campaign)
        {
            var book = new HintBook(campaign);

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

                book.Parse(text, name);
            }

            return book;
        }

        void Parse(string json, string file)
        {
            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
            }
            catch (JsonException bad)
            {
                _problems.Add(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
                return;
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    _problems.Add(new ContentProblem(
                        file, "", $"a hints file is a JSON object and this is a {Named(root.ValueKind)}"));
                    return;
                }

                foreach (JsonProperty property in root.EnumerateObject())
                    if (property.Name != "hints")
                        _problems.Add(new ContentProblem(
                            file, property.Name,
                            $"a hints file has no '{property.Name}' - it has hints, and nothing else"));

                if (!root.TryGetProperty("hints", out JsonElement list) ||
                    list.ValueKind != JsonValueKind.Array)
                {
                    _problems.Add(new ContentProblem(
                        file, "hints",
                        "a hints file is a list of problems - { \"hints\": [ { \"id\": " +
                        "\"the_stair_door\", \"rungs\": [ \"hinges_are_new\", " +
                        "\"somebody_replaced_it\", \"the_pins_lift_out\" ] } ] }"));
                    return;
                }

                int at = 0;

                foreach (JsonElement entry in list.EnumerateArray()) One(entry, file, $"hints[{at++}]");
            }
        }

        static readonly string[] Fields = { "id", "rungs" };

        void One(JsonElement entry, string file, string where)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                _problems.Add(new ContentProblem(
                    file, where, $"a hint is an object and this is a {Named(entry.ValueKind)}"));
                return;
            }

            foreach (JsonProperty property in entry.EnumerateObject())
                if (Array.IndexOf(Fields, property.Name) < 0)
                    _problems.Add(new ContentProblem(
                        file, $"{where}.{property.Name}",
                        $"a hint has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));

            if (!entry.TryGetProperty("id", out JsonElement id) ||
                id.ValueKind != JsonValueKind.String || !ContentId.IsLocal(id.GetString()))
            {
                _problems.Add(new ContentProblem(
                    file, $"{where}.id",
                    "a hint needs the problem it is about - lowercase a-z, 0-9 and underscore. " +
                    "The ladder counts asks per problem, so this is what it counts"));
                return;
            }

            if (!entry.TryGetProperty("rungs", out JsonElement rungs) ||
                rungs.ValueKind != JsonValueKind.Array)
            {
                _problems.Add(new ContentProblem(
                    file, $"{where}.rungs",
                    $"a hint needs its {HintLadder.Rungs} rungs, as beat ids - an observation, a " +
                    "nudge, then something close to the answer"));
                return;
            }

            var named = new List<string>();
            int at = 0;

            foreach (JsonElement rung in rungs.EnumerateArray())
            {
                if (rung.ValueKind != JsonValueKind.String || !ContentId.IsLocal(rung.GetString()))
                {
                    _problems.Add(new ContentProblem(
                        file, $"{where}.rungs[{at}]",
                        $"'{Shown(rung)}' is not a beat id - a rung names a beat in this " +
                        "campaign's beats/ folder, so every companion has to phrase it"));
                    return;
                }

                named.Add(rung.GetString());
                at++;
            }

            if (named.Count != HintLadder.Rungs)
            {
                _problems.Add(new ContentProblem(
                    file, $"{where}.rungs",
                    $"this hint has {named.Count} rungs and a ladder has {HintLadder.Rungs} - an " +
                    "observation, a nudge, then something close to the answer. Fewer and asking " +
                    "twice repeats itself; more and the third ask is not the last one"));
                return;
            }

            if (_hints.ContainsKey(id.GetString()))
            {
                _problems.Add(new ContentProblem(
                    file, $"{where}.id",
                    $"'{id.GetString()}' already has a ladder in this campaign"));
                return;
            }

            _hints[id.GetString()] = new Hint(id.GetString(), named);
        }

        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        static string Shown(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Undefined => "nothing",
            _ => value.ToString(),
        };

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();

        public override string ToString() =>
            $"{_hints.Count} hints" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }

    public sealed class Hint
    {
        public Hint(string id, IReadOnlyList<string> rungs)
        {
            Id = id;
            Rungs = rungs ?? Array.Empty<string>();
        }

        // the problem asked about, which is what HintLadder counts
        public string Id { get; }

        // beat ids, in order: observation, nudge, answer
        public IReadOnlyList<string> Rungs { get; }

        // the beat for a rung, 1-based as the ladder counts them; null past the top
        public string Beat(int rung) =>
            rung >= 1 && rung <= Rungs.Count ? Rungs[rung - 1] : null;

        public override string ToString() => $"{Id}: {string.Join(" -> ", Rungs)}";
    }
}

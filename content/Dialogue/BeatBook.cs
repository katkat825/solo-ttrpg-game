using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;

namespace Content.Dialogue
{
    // a campaign's beats/ folder. A file holds a LIST of beats rather than one apiece: the spine is
    // read and edited as a whole, and a hundred two-line files is a worse writing surface than one
    // hundred-line file, which is the thing W is meant to get right.
    public sealed class BeatBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Beat> _beats = new Dictionary<string, Beat>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        BeatBook(string campaign) => Campaign = campaign ?? "";

        public string Campaign { get; }

        public IReadOnlyCollection<string> Ids => _beats.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _beats.ContainsKey(id);

        public Beat Of(string id) => id != null && _beats.TryGetValue(id, out Beat beat) ? beat : null;

        public IEnumerable<Beat> All =>
            _beats.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _beats[i]);

        public int Count => _beats.Count;

        // Every phrasing the spine CAN use: the DM's floor, and one per beat per voice.
        //
        // None of these is demanded of a campaign - a campaign cannot write for a companion that
        // was published after it. What is demanded is that each beat is deliverable, which is
        // Reaches() below and which the dialogue check enforces. This list exists so the locale
        // audit knows a beat key in a CSV is a real key and not an orphan.
        public IEnumerable<string> KeysFor(IEnumerable<string> speakers)
        {
            foreach (Beat beat in All)
            {
                yield return beat.DmKey(Campaign);

                foreach (string speaker in speakers ?? Array.Empty<string>())
                    yield return beat.KeyFor(speaker, Campaign);
            }
        }

        // Is this beat deliverable to EVERY player? The DM can always say it, so a DM phrasing
        // settles it on its own; otherwise every voice on the shelf needs its own.
        public bool Reaches(Beat beat, IEnumerable<string> voices, Func<string, bool> written) =>
            beat != null && written != null
            && (written(beat.DmKey(Campaign))
                || (voices ?? Array.Empty<string>()).All(v => written(beat.KeyFor(v, Campaign))));

        // which voices are short of it, for a problem to name
        public IEnumerable<string> Silent(Beat beat, IEnumerable<string> voices,
                                          Func<string, bool> written) =>
            beat == null || written == null
                ? Array.Empty<string>()
                : (voices ?? Array.Empty<string>()).Where(v => !written(beat.KeyFor(v, Campaign)));


        public static BeatBook Read(string folder, string campaign)
        {
            var book = new BeatBook(campaign);

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
                        file, "", $"a beats file is a JSON object and this is a {Named(root.ValueKind)}"));
                    return;
                }

                foreach (JsonProperty property in root.EnumerateObject())
                    if (property.Name != "beats")
                        _problems.Add(new ContentProblem(
                            file, property.Name,
                            $"a beats file has no '{property.Name}' - it has beats, and nothing else"));

                if (!root.TryGetProperty("beats", out JsonElement list) ||
                    list.ValueKind != JsonValueKind.Array)
                {
                    _problems.Add(new ContentProblem(
                        file, "beats",
                        "a beats file is a list of intents - " +
                        "{ \"beats\": [ { \"id\": \"warn_bridge_trapped\", \"kind\": \"plot\" } ] }"));
                    return;
                }

                int at = 0;

                foreach (JsonElement entry in list.EnumerateArray()) One(entry, file, $"beats[{at++}]");
            }
        }

        static readonly string[] Fields = { "id", "kind", "note" };

        void One(JsonElement entry, string file, string where)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                _problems.Add(new ContentProblem(
                    file, where, $"a beat is an object and this is a {Named(entry.ValueKind)}"));
                return;
            }

            foreach (JsonProperty property in entry.EnumerateObject())
                if (Array.IndexOf(Fields, property.Name) < 0)
                    _problems.Add(new ContentProblem(
                        file, $"{where}.{property.Name}",
                        $"a beat has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));

            if (!entry.TryGetProperty("id", out JsonElement id) ||
                id.ValueKind != JsonValueKind.String || !ContentId.IsLocal(id.GetString()))
            {
                _problems.Add(new ContentProblem(
                    file, $"{where}.id",
                    "a beat needs an id - lowercase a-z, 0-9 and underscore. Every companion is " +
                    "keyed off it, so it is the name of the intent and not of a line"));
                return;
            }

            BeatKind kind = BeatKind.Plot;

            if (entry.TryGetProperty("kind", out JsonElement word))
            {
                if (word.ValueKind != JsonValueKind.String || !BeatKinds.TryWord(word.GetString(), out kind))
                {
                    _problems.Add(new ContentProblem(
                        file, $"{where}.kind",
                        $"'{Shown(word)}' is not a kind of beat - it is one of " +
                        $"{Vocabulary.Offer(BeatKinds.Words)}"));
                    return;
                }
            }

            string note = entry.TryGetProperty("note", out JsonElement said) &&
                          said.ValueKind == JsonValueKind.String
                ? said.GetString()
                : "";

            if (_beats.ContainsKey(id.GetString()))
            {
                _problems.Add(new ContentProblem(
                    file, $"{where}.id",
                    $"'{id.GetString()}' is already a beat in this campaign - one intent, written " +
                    "once, is the whole idea"));
                return;
            }

            _beats[id.GetString()] = new Beat(id.GetString(), kind, note);
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
            $"{_beats.Count} beats" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}

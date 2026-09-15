using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;

namespace Content.Campaigns
{
    public static class ManifestReader
    {
        // both file names are read; every campaign.json already written keeps working
        public const string PackFileName = "pack.json";

        public const string FileName = "campaign.json";

        // best first; Package looks in this order and complains if a folder has both
        public static readonly string[] FileNames = { PackFileName, FileName };

        // folder null skips the folder-matches-id check (a loose file has no folder)
        public static Read<Manifest> Parse(string json, string file, string folder = null)
        {
            var problems = new List<ContentProblem>();

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
                return Read<Manifest>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<Manifest>.Bad(new ContentProblem(
                        file, "",
                        $"a campaign manifest is a JSON object and this is a {Named(root.ValueKind)}"));

                string id = Id(root, file, folder, problems);
                PackKind kind = Kind(root, file, problems);
                int format = Format(root, file, problems);
                Version engine = Engine(root, file, problems);
                string author = Text(root, "author", file, problems);
                IReadOnlyList<string> tags = Tags(root, file, problems);
                string preview = Text(root, "preview", file, problems);
                IReadOnlyList<string> needs = Dependencies(root, id, file, problems);
                IReadOnlyList<Chapter> chapters = Chapters(root, kind, file, problems);
                string start = Start(root, chapters, file, problems);

                Unknown(root, file, problems);

                if (problems.Count > 0) return Read<Manifest>.Bad(problems);

                return Read<Manifest>.Good(
                    new Manifest(id, kind, format, engine, author, tags, preview, needs,
                                 chapters, start));
            }
        }

        static readonly string[] Fields =
        {
            "id", "kind", "format", "engine", "author", "tags", "preview", "dependencies",
            "chapters", "start",
        };

        static void Unknown(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(Fields, property.Name) >= 0) continue;

                // title/name/description get their own sentence; it's the mistake every modder makes
                if (property.Name == "title" || property.Name == "name" ||
                    property.Name == "description")
                {
                    string aspect = property.Name == "description" ? "description" : "name";

                    problems.Add(new ContentProblem(
                        file, property.Name,
                        $"a campaign's {property.Name} is not written here - it is a localized " +
                        "string, so it lives in this campaign's locale/ CSV under " +
                        $"'campaign.<id>.{aspect}' and is derived from the id rather than listed."));
                    continue;
                }

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"a campaign has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));
            }
        }

        static string Id(JsonElement root, string file, string folder, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    "a campaign needs an id, as a string - it is the namespace everything in the " +
                    "folder registers under"));
                return null;
            }

            string id = value.GetString() ?? "";

            if (!ContentId.IsCampaign(id))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"'{id}' is not a campaign id - lowercase a-z, 0-9 and underscore, and no " +
                    "dots. It becomes a segment of every key this campaign emits"));
                return null;
            }

            // folder and id must match; picking a winner would silently re-scope every key
            if (folder != null && !string.Equals(folder, id, StringComparison.Ordinal))
                problems.Add(new ContentProblem(
                    file, "id",
                    $"this campaign calls itself '{id}' and its folder is called '{folder}' - " +
                    "they have to match, because the folder is how a player finds it and the id " +
                    "is how every key in it is spelled"));

            return id;
        }

        // optional in campaign.json (defaults to campaign), required in pack.json
        static PackKind Kind(JsonElement root, string file, List<ContentProblem> problems)
        {
            bool isPackFile = string.Equals(file, PackFileName, StringComparison.Ordinal);

            if (!root.TryGetProperty("kind", out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null)
            {
                if (isPackFile)
                    problems.Add(new ContentProblem(
                        file, "kind",
                        $"a {PackFileName} says what kind of pack it is - it is " +
                        $"{Vocabulary.Offer<PackKind>()}. ({FileName} may leave it out and is " +
                        "taken to be a campaign)"));

                return PackKind.Campaign;
            }

            if (value.ValueKind == JsonValueKind.String &&
                Vocabulary.TryWord(value.GetString(), out PackKind kind))
            {
                return kind;
            }

            problems.Add(new ContentProblem(
                file, "kind",
                $"'{Shown(value)}' is not a kind of pack - it is {Vocabulary.Offer<PackKind>()}"));

            return PackKind.Campaign;
        }

        static int Format(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("format", out JsonElement value) ||
                value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int format))
            {
                problems.Add(new ContentProblem(
                    file, "format",
                    "a campaign needs a content format version, as a whole number - this build " +
                    $"writes {ContentFormat.Current}"));
                return 0;
            }

            if (!ContentFormat.CanRead(format))
            {
                problems.Add(new ContentProblem(file, "format", ContentFormat.WhyNot(format)));
                return 0;
            }

            return format;
        }

        static Version Engine(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("engine", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String ||
                !Version.TryParse(value.GetString(), out Version engine))
            {
                problems.Add(new ContentProblem(
                    file, "engine",
                    "a campaign needs the oldest engine that can play it, as a string like " +
                    $"\"{Core.EngineVersion.Current}\""));
                return null;
            }

            // refused rather than half-played: a missing rule would look right and be wrong mid-fight
            if (!Core.EngineVersion.Satisfies(engine))
                problems.Add(new ContentProblem(
                    file, "engine",
                    $"this campaign needs engine {engine} and this is {Core.EngineVersion.Current} " +
                    "- update the game"));

            return engine;
        }

        static string Text(JsonElement root, string field, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value)) return "";

            if (value.ValueKind == JsonValueKind.Null) return "";

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(file, field, $"'{Shown(value)}' is not a string"));
                return "";
            }

            return value.GetString() ?? "";
        }

        static IReadOnlyList<string> Tags(JsonElement root, string file, List<ContentProblem> problems)
        {
            var tags = new List<string>();

            if (!root.TryGetProperty("tags", out JsonElement value)) return tags;

            if (value.ValueKind == JsonValueKind.Null) return tags;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "tags", "tags is a list of words, like [ \"undead\", \"short\" ]"));
                return tags;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"tags[{at++}]";

                if (entry.ValueKind != JsonValueKind.String)
                {
                    problems.Add(new ContentProblem(file, where, $"'{Shown(entry)}' is not a word"));
                    continue;
                }

                string tag = entry.GetString() ?? "";

                if (!ContentId.IsLocal(tag))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{tag}' is not a tag - lowercase a-z, 0-9 and underscore. A tag is a " +
                        "word a storefront filters on, not a phrase a player reads"));
                    continue;
                }

                if (tags.Contains(tag))
                {
                    problems.Add(new ContentProblem(file, where, $"'{tag}' is listed twice"));
                    continue;
                }

                tags.Add(tag);
            }

            return tags;
        }

        // pack ids only; whether they're installed is the shelf's question, not this reader's
        static IReadOnlyList<string> Dependencies(JsonElement root, string id, string file,
                                                  List<ContentProblem> problems)
        {
            var needs = new List<string>();

            if (!root.TryGetProperty("dependencies", out JsonElement value)) return needs;

            if (value.ValueKind == JsonValueKind.Null) return needs;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "dependencies",
                    "dependencies is a list of the ids of packs this one needs, like " +
                    "[ \"grimdark_minis\" ]"));
                return needs;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"dependencies[{at++}]";

                if (entry.ValueKind != JsonValueKind.String ||
                    !ContentId.IsCampaign(entry.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{Shown(entry)}' is not a pack id - lowercase a-z, 0-9 and underscore, " +
                        "the same spelling as the pack's own id"));
                    continue;
                }

                string needed = entry.GetString();

                // self-dependency would loop the resolver; it's a typo
                if (string.Equals(needed, id, StringComparison.Ordinal))
                {
                    problems.Add(new ContentProblem(
                        file, where, $"'{needed}' is this pack - it cannot depend on itself"));
                    continue;
                }

                if (needs.Contains(needed))
                {
                    problems.Add(new ContentProblem(file, where, $"'{needed}' is listed twice"));
                    continue;
                }

                needs.Add(needed);
            }

            return needs;
        }

        static IReadOnlyList<Chapter> Chapters(JsonElement root, PackKind kind, string file,
                                               List<ContentProblem> problems)
        {
            var chapters = new List<Chapter>();

            bool playable = kind == PackKind.Campaign || kind == PackKind.Mixed;

            if (!root.TryGetProperty("chapters", out JsonElement value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                if (playable)
                    problems.Add(new ContentProblem(
                        file, "chapters",
                        "a campaign needs chapters, as a list in the order they are played - " +
                        "[ { \"id\": \"the_yard\", \"encounters\": [ \"ash_yard\" ] } ]"));

                return chapters;
            }

            if (!playable)
            {
                problems.Add(new ContentProblem(
                    file, "chapters",
                    $"a {kind.ToString().ToLowerInvariant()} pack has nothing to play, so it has " +
                    "no chapters - say \"kind\": \"mixed\" if this is a campaign that also " +
                    "ships its own art"));

                return chapters;
            }

            if (value.GetArrayLength() == 0)
                problems.Add(new ContentProblem(
                    file, "chapters", "a campaign with no chapters in it has nothing to play"));

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"chapters[{at++}]";

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where, $"a chapter is an object and this is a {Named(entry.ValueKind)}"));
                    continue;
                }

                Chapter chapter = OneChapter(entry, file, where, problems);

                if (chapter == null) continue;

                if (chapters.Exists(c => c.Id == chapter.Id))
                {
                    problems.Add(new ContentProblem(
                        file, where, $"there are two chapters called '{chapter.Id}'"));
                    continue;
                }

                chapters.Add(chapter);
            }

            return chapters;
        }

        static readonly string[] ChapterFields = { "id", "encounters" };

        static Chapter OneChapter(JsonElement entry, string file, string where,
                                  List<ContentProblem> problems)
        {
            foreach (JsonProperty property in entry.EnumerateObject())
                if (Array.IndexOf(ChapterFields, property.Name) < 0)
                    problems.Add(new ContentProblem(
                        file, $"{where}.{property.Name}",
                        $"a chapter has no '{property.Name}' - it has " +
                        $"{Vocabulary.Offer(ChapterFields)}. Its title is a localized string and " +
                        "lives in this campaign's locale/ CSV"));

            if (!entry.TryGetProperty("id", out JsonElement idValue) ||
                idValue.ValueKind != JsonValueKind.String ||
                !ContentId.IsLocal(idValue.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.id",
                    "a chapter needs an id - lowercase a-z, 0-9 and underscore. It becomes a " +
                    "segment of the chapter's title key"));
                return null;
            }

            string id = idValue.GetString();
            var encounters = new List<string>();

            if (!entry.TryGetProperty("encounters", out JsonElement list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.encounters",
                    "a chapter needs its encounters, by id, in the order they are played"));
                return null;
            }

            int at = 0;

            foreach (JsonElement one in list.EnumerateArray())
            {
                string spot = $"{where}.encounters[{at++}]";

                if (one.ValueKind != JsonValueKind.String || !ContentId.IsLocal(one.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, spot, $"'{Shown(one)}' is not an encounter id"));
                    continue;
                }

                encounters.Add(one.GetString());
            }

            if (encounters.Count == 0)
                problems.Add(new ContentProblem(
                    file, $"{where}.encounters", $"chapter '{id}' has nothing in it"));

            return new Chapter(id, encounters);
        }

        static string Start(JsonElement root, IReadOnlyList<Chapter> chapters, string file,
                            List<ContentProblem> problems)
        {
            string first = chapters.Count > 0 ? chapters[0].Id : "";

            if (!root.TryGetProperty("start", out JsonElement value)) return first;

            if (value.ValueKind == JsonValueKind.Null) return first;

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(
                    file, "start", $"'{Shown(value)}' is not a chapter id"));
                return first;
            }

            string start = value.GetString() ?? "";

            if (chapters.Count > 0 && !HasChapter(chapters, start))
            {
                problems.Add(new ContentProblem(
                    file, "start",
                    $"this campaign starts at '{start}' and has no such chapter - it has " +
                    $"{Vocabulary.Offer(Names(chapters))}"));
                return first;
            }

            return start;
        }

        static bool HasChapter(IReadOnlyList<Chapter> chapters, string id)
        {
            foreach (Chapter chapter in chapters)
                if (chapter.Id == id) return true;

            return false;
        }

        static string[] Names(IReadOnlyList<Chapter> chapters)
        {
            var names = new string[chapters.Count];

            for (int i = 0; i < chapters.Count; i++) names[i] = chapters[i].Id;

            return names;
        }

        static string Shown(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Undefined => "nothing",
            _ => value.ToString(),
        };

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();
    }
}

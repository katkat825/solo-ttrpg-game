using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;

namespace Content.Minis
{
    public static class MiniReader
    {
        public const string Extension = ".json";

        // null/empty pack means the engine's own minis, kept un-prefixed
        public static Read<MiniManifest> Parse(string json, string file, string pack)
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
                return Read<MiniManifest>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<MiniManifest>.Bad(new ContentProblem(
                        file, "", $"a mini is a JSON object and this is a {Named(root.ValueKind)}"));

                string id = Id(root, file, pack, problems);
                string variant = Variant(root, file, problems);
                string model = Path(root, "model", file, problems);

                Either(variant, model, file, problems);

                Fit? fit = Fitting(root, file, problems);
                float? height = Height(root, fit, file, problems);
                float? foot = Number(root, "foot", file, problems);
                Tint tint = Tinted(root, file, problems);

                IReadOnlyDictionary<Motion, string> clips = Clips(root, file, problems);
                IReadOnlyDictionary<Motion, string> foley = Foley(root, file, problems);

                Unknown(root, file, problems);

                if (problems.Count > 0) return Read<MiniManifest>.Bad(problems);

                return Read<MiniManifest>.Good(
                    new MiniManifest(id, variant, model, fit, height, foot, tint, clips, foley));
            }
        }

        static readonly string[] Fields =
        {
            "id", "variant", "model", "fit", "height", "foot", "tint", "clips", "foley",
        };

        static void Unknown(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(Fields, property.Name) >= 0) continue;

                // name/title get their own sentence; it's what every author reaches for first
                if (property.Name == "name" || property.Name == "title")
                {
                    problems.Add(new ContentProblem(
                        file, property.Name,
                        $"a mini's {property.Name} is not written here - it is a localized " +
                        "string, so it lives in this pack's locale/ CSV under " +
                        "'mini.<pack>.<id>.name' and is derived from the id rather than listed."));
                    continue;
                }

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"a mini has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));
            }
        }

        static string Id(JsonElement root, string file, string pack, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(file, "id", "a mini needs an id, as a string"));
                return null;
            }

            string local = value.GetString() ?? "";

            if (!ContentId.IsLocal(local))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"'{local}' is not a mini id - lowercase a-z, 0-9 and underscore, and no " +
                    "dots. It becomes a segment of this mini's name key"));
                return null;
            }

            return string.IsNullOrEmpty(pack) ? local : ContentId.Scoped(pack, local);
        }

        // the one id here that may already be scoped: bare is a shared mini, dotted reaches another pack
        static string Variant(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("variant", out JsonElement value)) return "";

            if (value.ValueKind == JsonValueKind.Null) return "";

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(
                    file, "variant", $"'{Shown(value)}' is not a mini id"));
                return "";
            }

            string id = value.GetString() ?? "";

            if (ContentId.IsLocal(id) || (ContentId.IsScoped(id) && ContentId.NamesSomethingKeyable(id)))
                return id;

            problems.Add(new ContentProblem(
                file, "variant",
                $"'{id}' is not a mini id - either one of the shared minis by its bare name, or " +
                "another pack's as '<pack>.<mini>'"));

            return "";
        }

        // exactly one source: both is two figures under one id, neither is nothing to stand
        static void Either(string variant, string model, string file, List<ContentProblem> problems)
        {
            bool hasVariant = !string.IsNullOrEmpty(variant);
            bool hasModel = !string.IsNullOrEmpty(model);

            if (hasVariant && hasModel)
            {
                problems.Add(new ContentProblem(
                    file, "model",
                    "this mini has both a 'variant' and a 'model', so it is two figures under one " +
                    "id - a variant re-dresses a mini the game already has, a model brings a new " +
                    "one. Keep whichever was meant"));
                return;
            }

            if (!hasVariant && !hasModel)
                problems.Add(new ContentProblem(
                    file, "",
                    "a mini needs either a 'variant' - the id of a mini to re-dress - or a " +
                    "'model' - a file in this pack. With neither there is nothing to stand on " +
                    "the board"));
        }

        // a path that cannot leave the pack; ../../etc or C:\ is the difference between a mod and an exploit
        static string Path(JsonElement root, string field, string file,
                           List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value)) return "";

            if (value.ValueKind == JsonValueKind.Null) return "";

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(file, field, $"'{Shown(value)}' is not a path"));
                return "";
            }

            string path = (value.GetString() ?? "").Trim();

            if (path.Length == 0)
            {
                problems.Add(new ContentProblem(file, field, "this is empty"));
                return "";
            }

            if (!Inside(path))
            {
                problems.Add(new ContentProblem(
                    file, field,
                    $"'{path}' is not inside this pack - a pack's files are named with forward " +
                    "slashes, relative to the pack's own folder, and cannot climb out of it " +
                    "with '..' or start from a drive or a slash"));
                return "";
            }

            return path;
        }

        // the check a stranger's file can't name a file that isn't theirs; a predicate, not a regex, on purpose
        public static bool Inside(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (path.IndexOf('\\') >= 0) return false;
            if (path.IndexOf(':') >= 0) return false;
            if (path[0] == '/') return false;

            foreach (string segment in path.Split('/'))
            {
                if (segment.Length == 0) return false;
                if (segment == "." || segment == "..") return false;
            }

            return true;
        }

        // null (not Fit.Cell, the enum zero) so a variant can inherit; the default is applied at the end of the chain
        static Fit? Fitting(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("fit", out JsonElement value)) return null;

            if (value.ValueKind == JsonValueKind.Null) return null;

            if (value.ValueKind == JsonValueKind.String &&
                Vocabulary.TryWord(value.GetString(), out Fit fit))
                return fit;

            problems.Add(new ContentProblem(
                file, "fit",
                $"'{Shown(value)}' is not a way to fit a mini - it is {Vocabulary.Offer<Fit>()}. " +
                "'cell' makes it as wide as a square, 'height' makes it exactly as tall as you say"));

            return null;
        }

        static float? Height(JsonElement root, Fit? fit, string file, List<ContentProblem> problems)
        {
            float? height = Number(root, "height", file, problems);

            if (fit == Fit.Cell && height.HasValue)
            {
                problems.Add(new ContentProblem(
                    file, "height",
                    "this mini is fitted to the cell, so its height is whatever the model's " +
                    "proportions make it - drop the \"fit\" line to be fitted by height instead, " +
                    "or take the height out"));

                return null;
            }

            if (fit == Fit.Height && !height.HasValue)
                problems.Add(new ContentProblem(
                    file, "height",
                    "this mini is fitted by height and has none - a figure is about 0.075 " +
                    "(75 mm) and the board's squares are 0.06"));

            return height;
        }

        static float? Number(JsonElement root, string field, string file,
                             List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value)) return null;

            if (value.ValueKind == JsonValueKind.Null) return null;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out float number))
            {
                problems.Add(new ContentProblem(
                    file, field, $"'{Shown(value)}' is not a measurement in metres"));
                return null;
            }

            if (number < -MiniCaps.LargestPiece || number > MiniCaps.LargestPiece)
                problems.Add(new ContentProblem(
                    file, field,
                    $"{number} is not a size on a table - everything here is in METRES, so a " +
                    $"figure is about 0.075 and nothing is bigger than {MiniCaps.LargestPiece}"));

            return number;
        }

        static Tint Tinted(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("tint", out JsonElement value)) return Tint.None;

            if (value.ValueKind == JsonValueKind.Null) return Tint.None;

            if (value.ValueKind == JsonValueKind.String &&
                Tint.TryParse(value.GetString(), out Tint tint))
                return tint;

            problems.Add(new ContentProblem(
                file, "tint",
                $"'{Shown(value)}' is not a colour - it is \"#RRGGBB\", six hex digits. A palette " +
                "name will not work here: the palette is stated once, in tools/palette.ps1, and " +
                "naming its colours in content would be a second copy of it (see Tint.cs)"));

            return Tint.None;
        }

        static IReadOnlyDictionary<Motion, string> Clips(JsonElement root, string file,
                                                        List<ContentProblem> problems) =>
            Motions(root, "clips", file, problems, (motion, text, where) =>
            {
                // a clip name is the model's own (Walk_A, Armature|Death); not held to the id grammar
                if (text.Trim().Length > 0) return text.Trim();

                problems.Add(new ContentProblem(
                    file, where, $"'{motion}' names no clip - take the line out instead"));

                return null;
            });

        static IReadOnlyDictionary<Motion, string> Foley(JsonElement root, string file,
                                                        List<ContentProblem> problems) =>
            Motions(root, "foley", file, problems, (motion, text, where) =>
            {
                string folder = text.Trim();

                if (Inside(folder)) return folder;

                problems.Add(new ContentProblem(
                    file, where,
                    $"'{folder}' is not a folder inside this pack - foley is a FOLDER of wavs " +
                    "rather than one file, because the folder is the list and a set of three " +
                    "set-downs is what keeps a piece from sounding like a loop"));

                return null;
            });

        static IReadOnlyDictionary<Motion, string> Motions(
            JsonElement root, string field, string file, List<ContentProblem> problems,
            Func<Motion, string, string, string> value)
        {
            var found = new Dictionary<Motion, string>();

            if (!root.TryGetProperty(field, out JsonElement map)) return found;

            if (map.ValueKind == JsonValueKind.Null) return found;

            if (map.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, field,
                    $"{field} is an object keyed by what the piece is doing - it takes " +
                    $"{Vocabulary.Offer<Motion>()}"));
                return found;
            }

            foreach (JsonProperty property in map.EnumerateObject())
            {
                string where = $"{field}.{property.Name}";

                if (!Vocabulary.TryWord(property.Name, out Motion motion))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{property.Name}' is not something a piece does - it is " +
                        $"{Vocabulary.Offer<Motion>()}"));
                    continue;
                }

                if (found.ContainsKey(motion))
                {
                    problems.Add(new ContentProblem(file, where, $"'{property.Name}' is listed twice"));
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    problems.Add(new ContentProblem(
                        file, where, $"'{Shown(property.Value)}' is not a name"));
                    continue;
                }

                string said = value(motion, property.Value.GetString() ?? "", where);

                if (said != null) found[motion] = said;
            }

            return found;
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

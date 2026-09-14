using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;

namespace Content.Minis
{
    // JSON IN, A MINI OUT, AND EVERY REASON IT IS NOT ONE (MINIS_AND_ART.md A0, A1).
    //
    // The same hand-rolled shape as `StatblockReader` and `ItemReader`, and for the same reason
    // they both state: deserializing into a class gives an author `null` fields and a stack trace,
    // and what they need is the file, the field and a sentence. Every problem is collected, never
    // thrown, and the read comes back with all of them at once.
    //
    // THE ID IN THE FILE IS THE LOCAL ONE, exactly as a statblock's is: a pack writes
    // `"id": "oldbones"` and the loader scopes it to `grimdark.oldbones`, because an author should
    // not have to type their own pack's name into every file and a forked pack should not need
    // every file edited.
    //
    // A PATH IS A FIELD THIS READER TAKES SERIOUSLY, and it is the only schema in the project
    // where that is true. Every other file names ideas - an id, a die, a tier - and this one names
    // FILES, which is a way out of the pack folder if nobody is looking. `Inside` below is that
    // look: forward slashes, relative, no drive, no `..`, no leading separator. Refused here, at
    // the field, rather than at the point where something would have opened it.
    public static class MiniReader
    {
        public const string Extension = ".json";

        // <paramref name="pack"/> is the pack folder's unique id, or null/empty for the engine's
        // own minis - which keep their un-prefixed ids (CONVENTIONS.md section 7)
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

                // THE ONE THAT DESERVES ITS OWN SENTENCE, the way `campaign.json` gives one to
                // `title`. A name in a mini manifest is what every author will reach for first,
                // and "a mini has no 'name'" is a true answer that teaches nothing
                if (property.Name == "name" || property.Name == "title")
                {
                    problems.Add(new ContentProblem(
                        file, property.Name,
                        $"a mini's {property.Name} is not written here - it is a localized " +
                        "string, so it lives in this pack's locale/ CSV under " +
                        "'mini.<pack>.<id>.name' and is derived from the id rather than listed. " +
                        "See CONVENTIONS.md on keys"));
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

        // THE ONE ID IN THIS FILE THAT MAY ALREADY BE SCOPED, because a variant can be OF another
        // pack's mini as well as of a shared one. A bare word means "one of the shared roster's",
        // which is the overwhelmingly common case and the one A0 is written around; a dotted one
        // reaches across to a pack this one had better depend on (A4)
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

        // EXACTLY ONE SOURCE, and the sentence says which phase each of them is. An author who
        // wrote both has described two different figures under one id; an author who wrote neither
        // has described nothing at all, and silence is the worst answer to either
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

        // A PATH THAT CANNOT LEAVE THE PACK. Content is data and a pack is a folder; a manifest
        // that could write `../../../etc` or `C:\` would be a content file reaching into the
        // machine, which is the difference between a mod and an exploit (MODDING.md section 5)
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

        // the whole of it, and it is worth keeping in one readable predicate rather than a regex:
        // this is the check that a stranger's file cannot name a file that is not theirs
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

        // NULL MEANS THE FILE DID NOT SAY, which a variant reads as "whatever I am a variant of".
        // `Fit.Cell` is the enum's zero and would swallow that distinction, so it is not the
        // fallback here - `MiniRegistry` supplies the default at the END of a chain, once, where
        // there is nothing left to inherit from
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

        // IN METRES, because everything on this table is - a 60 mm square is 0.06 and a miniature
        // is 0.075. Named `height` in the file rather than `size`, because that is what it is.
        //
        // A HEIGHT ON ITS OWN IMPLIES `"fit": "height"`, which is the whole of A0's first gesture:
        // "the shipped skeleton, but a head taller" is one line, and making an author write the
        // `fit` that the height already implies would be making them say it twice
        static float? Height(JsonElement root, Fit? fit, string file, List<ContentProblem> problems)
        {
            float? height = Number(root, "height", file, problems);

            if (fit == Fit.Cell && height.HasValue)
            {
                // a height that is silently ignored is a mini that is the wrong size for a reason
                // its author cannot see, so the contradiction is named rather than resolved
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

            // A MINI THE SIZE OF THE ROOM is the failure a millimetre/metre mix-up makes, and it
            // is the easiest mistake in this file: 75 rather than 0.075 is a figure taller than
            // the house. Held to something a table could hold
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
                // a clip name is the model's own, written by whoever exported it, so it is NOT
                // held to the id grammar - "Walk_A", "Armature|Death" and worse are all real
                // names out of real exporters, and refusing them would refuse the model
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

        // one shape for both, because they are the same shape: the motion vocabulary on the left
        // and something the pack supplies on the right. The words are DERIVED from `Motion`, so a
        // sixth motion is authorable the day it exists
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

using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;

namespace Content.Dialogue
{
    public static class BarkReader
    {
        // the size of the bank a writer would sit down to fill; CORE_RULES.md section 12 asks for about forty Snags
        public const int MostLinesInABank = 999;

        public static Read<BarkBank> Parse(string json, string file)
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
                return Read<BarkBank>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<BarkBank>.Bad(new ContentProblem(
                        file, "",
                        $"a bark bank is a JSON object and this is a {Named(root.ValueKind)}"));

                Unknown(root, file, problems);

                string speaker = Speaker(root, file, problems);
                IReadOnlyDictionary<Bark, int> banks = Banks(root, file, problems);
                bool reads = Reads(root, file, problems);

                if (problems.Count > 0) return Read<BarkBank>.Bad(problems);

                if (banks.Count == 0)
                    return Read<BarkBank>.Bad(new ContentProblem(
                        file, "banks",
                        $"'{speaker}' has no barks at all, so it is a voice that never speaks - " +
                        $"give it a bank ({string.Join(", ", Barks.Words)}), or delete the file"));

                return Read<BarkBank>.Good(new BarkBank(speaker, banks, reads));
            }
        }

        static readonly string[] Fields = { "speaker", "banks", "reads_the_throw" };

        static void Unknown(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(Fields, property.Name) >= 0) continue;

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"a bark bank has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));
            }
        }

        // a creature, never a class: the class picks the companion and the companion owns the voice
        static string Speaker(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("speaker", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "speaker",
                    "a bark bank needs the creature that says them - 'wolf', not 'barbarian'. " +
                    "The class picks the companion; the companion owns the voice"));
                return null;
            }

            return value.GetString();
        }

        static IReadOnlyDictionary<Bark, int> Banks(JsonElement root, string file,
                                                    List<ContentProblem> problems)
        {
            var banks = new Dictionary<Bark, int>();

            if (!root.TryGetProperty("banks", out JsonElement value) ||
                value.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, "banks",
                    "a bark bank needs its banks - { \"snag\": 40, \"trouble\": 12 }. The number " +
                    "is how many lines the locale holds, and the lines are numbered from 1"));
                return banks;
            }

            foreach (JsonProperty property in value.EnumerateObject())
            {
                string where = "banks." + property.Name;

                if (!Content.Dialogue.Barks.TryWord(property.Name, out Bark situation))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{property.Name}' is not something the table can do - it is one of " +
                        $"{Vocabulary.Offer(Content.Dialogue.Barks.Words)}. The list is the " +
                        "engine's, because a situation nothing raises is a bark nothing says"));
                    continue;
                }

                if (banks.ContainsKey(situation))
                {
                    problems.Add(new ContentProblem(
                        file, where, $"'{situation.Word()}' is counted twice in this file"));
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Number ||
                    !property.Value.TryGetInt32(out int lines))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{Shown(property.Value)}' is not a count - a bank says how many lines " +
                        "the locale holds for it, as a whole number"));
                    continue;
                }

                if (lines < 1 || lines > MostLinesInABank)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"{lines} lines is not a bank - it is 1 to {MostLinesInABank}. A bank of " +
                        "none is the field left out"));
                    continue;
                }

                banks[situation] = lines;
            }

            return banks;
        }

        static bool Reads(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("reads_the_throw", out JsonElement value)) return false;

            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;

            problems.Add(new ContentProblem(
                file, "reads_the_throw",
                $"'{Shown(value)}' is not true or false - it says whether this voice, rather than " +
                "the DM, tells you what the dice came to"));

            return false;
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

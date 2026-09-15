using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Core.Characters;
using Core.Dice;

namespace Content.Items
{
    // gear ids are a shared namespace, not scoped; a campaign's axe gets the engine's
    public static class ItemReader
    {
        public static Read<Gear> Parse(string json, string file)
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
                return Read<Gear>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<Gear>.Bad(new ContentProblem(
                        file, "", $"an item is a JSON object and this is a {Named(root.ValueKind)}"));

                string id = Id(root, file, problems);
                Die die = GearDie(root, file, problems);
                Skill supports = Supports(root, file, problems);
                int defense = Defense(root, file, problems);
                IReadOnlyList<Condition> inflicts = Inflicts(root, file, problems);

                Unknown(root, file, problems);

                if (problems.Count == 0 && die == Die.None && defense == 0 && inflicts.Count == 0)
                    problems.Add(new ContentProblem(
                        file, "",
                        "this item has no die, no defense and inflicts nothing, so carrying it " +
                        "changes nothing - give it one of the three"));

                if (problems.Count > 0) return Read<Gear>.Bad(problems);

                return Read<Gear>.Good(new Gear(id, die, supports, defense, inflicts));
            }
        }

        static readonly string[] Fields = { "id", "die", "supports", "defense", "inflicts" };

        static void Unknown(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(Fields, property.Name) >= 0) continue;

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"an item has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));
            }
        }

        static string Id(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) || value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(file, "id", "an item needs an id, as a string"));
                return null;
            }

            string id = value.GetString() ?? "";

            if (!ContentId.IsLocal(id))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"'{id}' is not an id - lowercase a-z, 0-9 and underscore, and no dots"));
                return null;
            }

            return id;
        }

        static Die GearDie(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("die", out JsonElement value)) return Die.None;

            if (value.ValueKind == JsonValueKind.Null) return Die.None;

            if (value.ValueKind != JsonValueKind.String || !Vocabulary.TryDie(value.GetString(), out Die die))
            {
                problems.Add(new ContentProblem(
                    file, "die",
                    $"'{Text(value)}' is not a die - it is one of {Vocabulary.Offer(Vocabulary.Dice)}"));
                return Die.None;
            }

            return die;
        }

        static Skill Supports(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("supports", out JsonElement value)) return Skill.None;

            if (value.ValueKind == JsonValueKind.Null) return Skill.None;

            if (value.ValueKind != JsonValueKind.String ||
                !Vocabulary.TryWord(value.GetString(), out Skill skill))
            {
                problems.Add(new ContentProblem(
                    file, "supports",
                    $"'{Text(value)}' is not a skill - it is one of {Vocabulary.Offer<Skill>()}"));
                return Skill.None;
            }

            return skill;
        }

        static int Defense(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("defense", out JsonElement value)) return 0;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
            {
                problems.Add(new ContentProblem(file, "defense", $"'{Text(value)}' is not a whole number"));
                return 0;
            }

            // Defense is the strongest balance lever there is; the range is deliberately tiny
            if (number < -MostArmourCanDo || number > MostArmourCanDo)
            {
                problems.Add(new ContentProblem(
                    file, "defense",
                    $"{number} is more than gear may move Defense. The range is " +
                    $"{-MostArmourCanDo} to {MostArmourCanDo}, because Defense is the strongest " +
                    "balance lever there is - +2 on the hero measures as " +
                    "double the win rate"));
                return 0;
            }

            return number;
        }

        // two either way, the measured span between a real fight and not a fight
        public const int MostArmourCanDo = 2;

        static IReadOnlyList<Condition> Inflicts(JsonElement root, string file, List<ContentProblem> problems)
        {
            var conditions = new List<Condition>();

            if (!root.TryGetProperty("inflicts", out JsonElement value)) return conditions;

            if (value.ValueKind == JsonValueKind.Null) return conditions;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "inflicts", "inflicts is a list of conditions, like [ \"reeling\" ]"));
                return conditions;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"inflicts[{at++}]";

                if (entry.ValueKind != JsonValueKind.String ||
                    !Vocabulary.TryWord(entry.GetString(), out Condition condition))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{Text(entry)}' is not a condition - it is one of {Vocabulary.Offer<Condition>()}"));
                    continue;
                }

                if (conditions.Contains(condition))
                {
                    problems.Add(new ContentProblem(file, where, $"'{entry.GetString()}' is listed twice"));
                    continue;
                }

                conditions.Add(condition);
            }

            return conditions;
        }

        static string Text(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Undefined => "nothing",
            _ => value.ToString(),
        };

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();
    }
}

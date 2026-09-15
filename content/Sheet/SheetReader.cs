using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Core.Characters;
using Core.Dice;

namespace Content.Sheet
{
    public static class SheetReader
    {
        // a race that moved a die three sizes would not be a race, it would be a class
        public const int MostSteps = 2;

        public static Read<TraitCard> Parse(string json, string file, string pack, Blank blank)
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
                return Read<TraitCard>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<TraitCard>.Bad(new ContentProblem(
                        file, "",
                        $"a {blank.Word()} is a JSON object and this is a {Named(root.ValueKind)}"));

                foreach (JsonProperty property in root.EnumerateObject())
                    if (Array.IndexOf(Fields, property.Name) < 0)
                        problems.Add(new ContentProblem(
                            file, property.Name,
                            $"a {blank.Word()} has no '{property.Name}' - it has " +
                            $"{Vocabulary.Offer(Fields)}. It has no vigor and no defense: those " +
                            "are the class's, and a sheet fills its blanks from cards that only " +
                            "ever nudge"));

                string local = Id(root, file, blank, problems);
                IReadOnlyDictionary<Attr, int> nudges = Nudges(root, file, problems);
                IReadOnlyDictionary<Skill, Die> skills = Skills(root, file, problems);

                if (problems.Count > 0) return Read<TraitCard>.Bad(problems);

                string id = ContentId.IsCampaign(pack) ? ContentId.Scoped(pack, local) : local;

                return Read<TraitCard>.Good(new TraitCard(id, blank, nudges, skills));
            }
        }

        static readonly string[] Fields = { "id", "nudges", "skills" };

        static string Id(JsonElement root, string file, Blank blank, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"a {blank.Word()} needs an id - lowercase a-z, 0-9 and underscore. It is what " +
                    "a finished sheet writes down, and what a save reads back"));
                return null;
            }

            return value.GetString();
        }

        // steps on the ladder, never die sizes, so two cards and a Condition compose in any order
        static IReadOnlyDictionary<Attr, int> Nudges(JsonElement root, string file,
                                                     List<ContentProblem> problems)
        {
            var nudges = new Dictionary<Attr, int>();

            if (!root.TryGetProperty("nudges", out JsonElement value)) return nudges;

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, "nudges",
                    "nudges is an object of attribute to steps - { \"might\": 1, \"grace\": -1 }. " +
                    "Steps up and down the ladder, not die sizes, because that is the only way two " +
                    "of them and a Condition can land in any order and mean the same thing"));
                return nudges;
            }

            foreach (JsonProperty property in value.EnumerateObject())
            {
                string where = "nudges." + property.Name;

                if (!Vocabulary.TryWord(property.Name, out Attr attribute))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{property.Name}' is not an attribute - it is one of " +
                        $"{Vocabulary.Offer<Attr>()}"));
                    continue;
                }

                if (nudges.ContainsKey(attribute))
                {
                    problems.Add(new ContentProblem(file, where, $"'{property.Name}' is nudged twice"));
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Number ||
                    !property.Value.TryGetInt32(out int steps))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{Shown(property.Value)}' is not a number of steps - a whole number, " +
                        $"{-MostSteps} to {MostSteps}"));
                    continue;
                }

                if (steps == 0)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        "a nudge of nothing does nothing - leave the attribute out instead, so " +
                        "the card says what it means"));
                    continue;
                }

                if (Math.Abs(steps) > MostSteps)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"{steps} steps is not a nudge - it is {-MostSteps} to {MostSteps}. " +
                        "Anything bigger is a class, and a class is a different file"));
                    continue;
                }

                nudges[attribute] = steps;
            }

            return nudges;
        }

        static IReadOnlyDictionary<Skill, Die> Skills(JsonElement root, string file,
                                                      List<ContentProblem> problems)
        {
            var skills = new Dictionary<Skill, Die>();

            if (!root.TryGetProperty("skills", out JsonElement value)) return skills;

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, "skills",
                    "skills is an object of name to die - { \"survival\": \"d4\" }. Untrained is " +
                    "the skill left out; a card never un-trains one the class already had"));
                return skills;
            }

            foreach (JsonProperty property in value.EnumerateObject())
            {
                string where = "skills." + property.Name;

                if (!Vocabulary.TryWord(property.Name, out Skill skill) || skill == Skill.None)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{property.Name}' is not a skill - it is one of " +
                        $"{Vocabulary.Offer<Skill>()}"));
                    continue;
                }

                if (skills.ContainsKey(skill))
                {
                    problems.Add(new ContentProblem(file, where, $"'{property.Name}' is trained twice"));
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String ||
                    !Vocabulary.TryDie(property.Value.GetString(), out Die die) || !die.IsReal())
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{Shown(property.Value)}' is not a die - trained runs " +
                        $"{Vocabulary.Offer(Vocabulary.Dice)}"));
                    continue;
                }

                skills[skill] = die;
            }

            return skills;
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

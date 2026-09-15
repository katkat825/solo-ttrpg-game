using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Core.Characters;
using Core.Resolution;

namespace Content.Kits
{
    public static class AbilityReader
    {
        public static Read<Ability> Parse(string json, string file, string pack)
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
                return Read<Ability>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message,
                    (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<Ability>.Bad(new ContentProblem(
                        file, "", $"an ability is a JSON object and this is a {Named(root.ValueKind)}"));

                string id = Id(root, file, pack, problems);

                Primitive primitive = Shape(root, file, problems);

                // a channel defaults to Heart and Channeling; a check has no default and must name both
                Attr? attribute = Word<Attr>(
                    root, "attribute", file, problems,
                    primitive == Primitive.Channel ? Core.Combat.Channeling.Attribute : null);

                Skill skill = Word<Skill>(
                    root, "skill", file, problems,
                    primitive == Primitive.Channel ? Core.Combat.Channeling.Skill : Skill.None)
                    ?? Skill.None;

                bool gear = Flag(root, "gear", primitive == Primitive.Channel, file, problems);

                Versus(root, file, problems, out int against, out bool againstDefense);

                Power(root, file, problems, out Magnitude magnitude, out int fixedMagnitude);

                Cost cost = Enumerated(root, "cost", primitive == Primitive.Channel ? Cost.Strain : Cost.None,
                                       file, problems);

                Target target = Enumerated(root, "target", primitive == Primitive.Channel ? Target.Sight : Target.Reach,
                                           file, problems);

                Condition? lands = Lands(root, file, problems);

                IReadOnlyList<Effect> onHit = Effects(root, Hit(primitive), primitive, file, problems);
                IReadOnlyList<Effect> onMiss = Effects(root, Miss(primitive), primitive, file, problems);

                Sense(primitive, onHit, onMiss, lands, file, problems);

                Unknown(root, primitive, file, problems);

                if (problems.Count > 0) return Read<Ability>.Bad(problems);

                return Read<Ability>.Good(new Ability(
                    id, primitive, attribute ?? Attr.Might, skill, gear, against, againstDefense,
                    magnitude, fixedMagnitude, cost, target, onHit, onMiss, lands));
            }
        }

        // a check writes pass/fail, a channel onHit/onMiss; same two lists, different vocabulary
        static string Hit(Primitive p) => p == Primitive.Check ? "pass" : "onHit";

        static string Miss(Primitive p) => p == Primitive.Check ? "fail" : "onMiss";

        static readonly string[] Common =
        {
            "id", "primitive", "attribute", "skill", "gear", "versus", "power", "cost", "target",
            "condition",
        };

        static void Unknown(JsonElement root, Primitive primitive, string file,
                            List<ContentProblem> problems)
        {
            string[] fields = Common.Concat(new[] { Hit(primitive), Miss(primitive) }).ToArray();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(fields, property.Name) >= 0) continue;

                // the other primitive's spelling, named as that: onHit on a check isn't a typo
                if (property.Name == Hit(Other(primitive)) || property.Name == Miss(Other(primitive)))
                {
                    problems.Add(new ContentProblem(
                        file, property.Name,
                        $"'{property.Name}' is how a {Vocabulary.NameOf(Other(primitive))} says it; " +
                        $"a {Vocabulary.NameOf(primitive)} uses '{Hit(primitive)}' and " +
                        $"'{Miss(primitive)}'"));
                    continue;
                }

                if (property.Name == "name" || property.Name == "description")
                {
                    problems.Add(new ContentProblem(
                        file, property.Name,
                        $"an ability's {property.Name} is not written in the file - it is a key, " +
                        "so it can be translated. Put it in this pack's locale/ CSV as " +
                        $"'{KeyLine(root, property.Name)}'"));
                    continue;
                }

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"an ability has no '{property.Name}' - it has {Vocabulary.Offer(fields)}"));
            }
        }

        static Primitive Other(Primitive p) =>
            p == Primitive.Check ? Primitive.Channel : Primitive.Check;

        static string KeyLine(JsonElement root, string aspect)
        {
            string id = root.TryGetProperty("id", out JsonElement value) &&
                        value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : "<id>";

            return Core.Localization.KeyConventions.Key(
                Core.Localization.KeyConventions.AbilityNs, id, aspect);
        }


        static Primitive Shape(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("primitive", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(
                    file, "primitive",
                    $"an ability says which of the engine's two hero actions it is - it is " +
                    $"{Vocabulary.Offer<Primitive>()}"));
                return Primitive.Check;
            }

            if (Vocabulary.TryWord(value.GetString(), out Primitive primitive)) return primitive;

            problems.Add(new ContentProblem(
                file, "primitive",
                $"'{Shown(value)}' is not one of the engine's two hero actions - this is a rules " +
                $"change, not an ability. An ability is {Vocabulary.Offer<Primitive>()} with its " +
                "parameters filled in; anything else needs a rule behind it, which is code and " +
                "not content"));

            return Primitive.Check;
        }


        static TEnum? Word<TEnum>(JsonElement root, string field, string file,
                                  List<ContentProblem> problems, TEnum? fallback)
            where TEnum : struct, Enum
        {
            if (!root.TryGetProperty(field, out JsonElement value))
            {
                if (fallback != null) return fallback;

                problems.Add(new ContentProblem(
                    file, field,
                    $"an ability needs a {field} - it is one of {Vocabulary.Offer<TEnum>()}"));
                return null;
            }

            if (value.ValueKind == JsonValueKind.String &&
                Vocabulary.TryWord(value.GetString(), out TEnum word))
                return word;

            problems.Add(new ContentProblem(
                file, field,
                $"'{Shown(value)}' is not a {field} - it is one of {Vocabulary.Offer<TEnum>()}"));

            return fallback;
        }

        static TEnum Enumerated<TEnum>(JsonElement root, string field, TEnum fallback, string file,
                                       List<ContentProblem> problems) where TEnum : struct, Enum =>
            Word<TEnum>(root, field, file, problems, fallback) ?? fallback;

        static bool Flag(JsonElement root, string field, bool fallback, string file,
                         List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value)) return fallback;

            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;

            problems.Add(new ContentProblem(
                file, field, $"'{Shown(value)}' is not true or false - {field} says whether the " +
                             "gear die is in the pool"));

            return fallback;
        }

        static void Versus(JsonElement root, string file, List<ContentProblem> problems,
                           out int against, out bool againstDefense)
        {
            against = Difficulty.Standard;
            againstDefense = false;

            if (!root.TryGetProperty("versus", out JsonElement value))
            {
                problems.Add(new ContentProblem(
                    file, "versus",
                    $"an ability says what it is measured against - a number, or \"{Defense}\" " +
                    "for the target's own Defense"));
                return;
            }

            if (value.ValueKind == JsonValueKind.String &&
                string.Equals(value.GetString(), Defense, StringComparison.Ordinal))
            {
                againstDefense = true;

                // against is unread when scored on Defense; zero it so no stale value disagrees later
                against = 0;
                return;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) && number > 0)
            {
                against = number;
                return;
            }

            problems.Add(new ContentProblem(
                file, "versus",
                $"'{Shown(value)}' is not a difficulty - a whole number above 0, or \"{Defense}\" " +
                "for the target's own Defense"));
        }

        public const string Defense = "defense";

        static void Power(JsonElement root, string file, List<ContentProblem> problems,
                          out Magnitude magnitude, out int fixedMagnitude)
        {
            magnitude = Magnitude.Impact;
            fixedMagnitude = 0;

            if (!root.TryGetProperty("power", out JsonElement value)) return;

            if (value.ValueKind == JsonValueKind.String &&
                Vocabulary.TryWord(value.GetString(), out Magnitude word) && word == Magnitude.Impact)
                return;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) && number > 0)
            {
                magnitude = Magnitude.Fixed;
                fixedMagnitude = number;
                return;
            }

            problems.Add(new ContentProblem(
                file, "power",
                $"'{Shown(value)}' is not a power - \"{Vocabulary.NameOf(Magnitude.Impact)}\" for " +
                "the die with the ring round it, or a whole number above 0 for a size that does " +
                "not vary"));
        }


        static IReadOnlyList<Effect> Effects(JsonElement root, string field, Primitive primitive,
                                             string file, List<ContentProblem> problems)
        {
            var effects = new List<Effect>();

            if (!root.TryGetProperty(field, out JsonElement value)) return effects;

            if (value.ValueKind == JsonValueKind.Null) return effects;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, field,
                    $"{field} is a list of what happens, like [ \"damage\" ] - it is any of " +
                    $"{Vocabulary.Offer<Effect>()}"));
                return effects;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"{field}[{at++}]";

                if (entry.ValueKind != JsonValueKind.String ||
                    !Vocabulary.TryWord(entry.GetString(), out Effect effect))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{Shown(entry)}' is not something the engine can do - it is one of " +
                        $"{Vocabulary.Offer<Effect>()}. An effect outside that list is a rules " +
                        "change, not an ability"));
                    continue;
                }

                if (effects.Contains(effect))
                {
                    problems.Add(new ContentProblem(file, where, $"'{Shown(entry)}' is listed twice"));
                    continue;
                }

                // a real effect the wrong primitive can't produce; open on a bolt would parse but do nothing
                if (!Allows(primitive, effect))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"a {Vocabulary.NameOf(primitive)} cannot '{Shown(entry)}' - that is a " +
                        $"{Vocabulary.NameOf(Other(primitive))}'s. A " +
                        $"{Vocabulary.NameOf(primitive)} does any of {Vocabulary.Offer(Allowed(primitive))}"));
                    continue;
                }

                effects.Add(effect);
            }

            return effects;
        }

        // in one place so the reader and the message it prints can't disagree
        static bool Allows(Primitive primitive, Effect effect) => primitive switch
        {
            Primitive.Channel => effect is Effect.Damage or Effect.Condition or Effect.Rough
                                          or Effect.Shove,

            _ => effect is Effect.Open or Effect.Rough or Effect.Recoil or Effect.Condition
                           or Effect.Shove or Effect.Steady,
        };

        static IEnumerable<string> Allowed(Primitive primitive) =>
            Enum.GetValues<Effect>().Where(e => Allows(primitive, e)).Select(Vocabulary.NameOf);

        // a channel names its condition; a check derives it from the attribute it used
        static Condition? Lands(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("condition", out JsonElement value)) return null;

            if (value.ValueKind == JsonValueKind.String &&
                Vocabulary.TryWord(value.GetString(), out Condition condition))
                return condition;


            problems.Add(new ContentProblem(
                file, "condition",
                $"'{Shown(value)}' is not a condition the engine has - it is one of " +
                $"{Vocabulary.Offer<Condition>()}. A new one is a rules change, not an ability"));

            return null;
        }


        // parses but makes no sense; each of these is a mistake with a silent symptom
        static void Sense(Primitive primitive, IReadOnlyList<Effect> onHit,
                          IReadOnlyList<Effect> onMiss, Condition? lands, string file,
                          List<ContentProblem> problems)
        {
            if (onHit.Count == 0)
                problems.Add(new ContentProblem(
                    file, Hit(primitive),
                    $"nothing happens when this lands, so it is a throw with no outcome - " +
                    $"{Hit(primitive)} is any of {Vocabulary.Offer(Allowed(primitive))}"));

            if (primitive == Primitive.Check && onMiss.Count == 0)
                problems.Add(new ContentProblem(
                    file, Miss(primitive),
                    "a failed check has to leave a different scene, not a repeated one - with " +
                    "nothing in 'fail' the player simply tries again, which is the one thing " +
                    "the rules forbid. Give it a cost, or let it through " +
                    "anyway and make the mess the consequence (the shipped door does both)"));

            if (primitive == Primitive.Channel && onHit.Contains(Effect.Condition) && lands == null)
                problems.Add(new ContentProblem(
                    file, "condition",
                    $"this leaves a condition and does not say which - it is one of " +
                    $"{Vocabulary.Offer<Condition>()}"));

            if (primitive == Primitive.Check && lands != null)
                problems.Add(new ContentProblem(
                    file, "condition",
                    "a check does not name its condition - the one it leaves is whichever presses " +
                    "on the attribute it used, which is the same rule a Trouble follows"));
        }


        static string Id(JsonElement root, string file, string pack, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) || value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(file, "id", "an ability needs an id, as a string"));
                return null;
            }

            string local = value.GetString() ?? "";

            if (!ContentId.IsLocal(local))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"'{local}' is not an id - lowercase a-z, 0-9 and underscore, and no dots " +
                    "(the pack's name is added for you)"));
                return null;
            }

            string scoped = ContentId.IsCampaign(pack) ? ContentId.Scoped(pack, local) : local;

            if (!ContentId.NamesSomethingKeyable(scoped))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"'{scoped}' cannot be made into a key - " +
                    Core.Localization.KeyConventions.Explain(
                        Core.Localization.KeyConventions.AbilityName(scoped))));
                return null;
            }

            return scoped;
        }

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();

        static string Shown(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Undefined => "nothing",
            _ => value.ToString(),
        };
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Items;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;

namespace Content.Monsters
{
    public static class StatblockReader
    {
        // null/empty campaign means the engine's own roster, kept un-prefixed
        public static Read<Statblock> Parse(string json, string file, string campaign)
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
                // the one case with a real line number; the text is still text here
                return Read<Statblock>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message,
                    (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<Statblock>.Bad(new ContentProblem(
                        file, "", $"a statblock is a JSON object and this is a {Named(root.ValueKind)}"));

                string id = Id(root, file, campaign, problems);
                Tier tier = Word(root, "tier", Tier.Rival, file, problems);
                int vigor = Count(root, "vigor", 1, file, problems);
                int defense = Count(root, "defense", 1, file, problems);

                IReadOnlyDictionary<Attr, Die> attributes = Dice<Attr>(root, "attributes", file, problems);
                IReadOnlyDictionary<Skill, Die> skills = Dice<Skill>(root, "skills", file, problems);

                Gear(root, file, problems, out string gearId, out Die gearDie);

                string behaviour = Behaviour(root, file, problems);
                string mini = Mini(root, file, problems);

                LootTable loot = root.TryGetProperty("loot", out JsonElement carried)
                    ? LootTable.Parse(carried, file, "loot", problems)
                    : LootTable.Nothing;

                Unknown(root, file, problems);

                if (problems.Count > 0) return Read<Statblock>.Bad(problems);

                return Read<Statblock>.Good(new Statblock(
                    id, tier, vigor, defense, attributes, skills, gearId, gearDie, behaviour, loot,
                    mini));
            }
        }

        // a field not listed here is a typo; silently ignoring it is a monster quietly not what its author wrote
        static readonly string[] Fields =
        {
            "id", "tier", "vigor", "defense", "attributes", "skills", "gear", "behaviour", "loot",
            "mini",
        };

        static void Unknown(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(Fields, property.Name) >= 0) continue;

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"a statblock has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));
            }
        }

        // bare is this pack's or the game's; a dotted id is another pack's and wants a dependencies line
        static string Mini(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("mini", out JsonElement value)) return "";

            if (value.ValueKind == JsonValueKind.Null) return "";

            if (value.ValueKind == JsonValueKind.String)
            {
                string id = value.GetString() ?? "";

                if (Content.Campaigns.ContentId.IsLocal(id) ||
                    (Content.Campaigns.ContentId.IsScoped(id) &&
                     Content.Campaigns.ContentId.NamesSomethingKeyable(id)))
                    return id;
            }

            problems.Add(new ContentProblem(
                file, "mini",
                $"'{Shown(value)}' is not a mini id - either one this pack's minis/ folder " +
                "declares, one of the ones the game ships, or another pack's as '<pack>.<mini>'"));

            return "";
        }

        static string Id(JsonElement root, string file, string campaign, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) || value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(file, "id", "a statblock needs an id, as a string"));
                return null;
            }

            string local = value.GetString() ?? "";

            if (!ContentId.IsLocal(local))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"'{local}' is not an id - lowercase a-z, 0-9 and underscore, and no dots " +
                    "(the campaign's name is added for you)"));
                return null;
            }

            string scoped = ContentId.IsCampaign(campaign) ? ContentId.Scoped(campaign, local) : local;

            // refused here rather than three layers away in a locale audit
            if (!ContentId.NamesSomethingKeyable(scoped))
            {
                problems.Add(new ContentProblem(
                    file, "id", $"'{scoped}' cannot be made into a key - {KeyProblem(scoped)}"));
                return null;
            }

            return scoped;
        }

        static string KeyProblem(string id) =>
            Core.Localization.KeyConventions.Explain(Core.Localization.KeyConventions.ActorName(id));

        static TEnum Word<TEnum>(JsonElement root, string field, TEnum fallback, string file,
                                 List<ContentProblem> problems) where TEnum : struct, Enum
        {
            if (!root.TryGetProperty(field, out JsonElement value)) return fallback;

            if (value.ValueKind != JsonValueKind.String || !Vocabulary.TryWord(value.GetString(), out TEnum word))
            {
                problems.Add(new ContentProblem(
                    file, field,
                    $"'{Text(value)}' is not a {field} - it is one of {Vocabulary.Offer<TEnum>()}"));
                return fallback;
            }

            return word;
        }

        static int Count(JsonElement root, string field, int least, string file,
                         List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value))
            {
                problems.Add(new ContentProblem(file, field, $"a statblock needs a {field}"));
                return least;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
            {
                problems.Add(new ContentProblem(file, field, $"'{Text(value)}' is not a whole number"));
                return least;
            }

            if (number < least)
            {
                problems.Add(new ContentProblem(file, field, $"{number} is less than {least}"));
                return least;
            }

            return number;
        }

        static IReadOnlyDictionary<TEnum, Die> Dice<TEnum>(
            JsonElement root, string field, string file, List<ContentProblem> problems)
            where TEnum : struct, Enum
        {
            var dice = new Dictionary<TEnum, Die>();

            if (!root.TryGetProperty(field, out JsonElement value)) return dice;

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, field, $"{field} is an object of name to die, like {{ \"might\": \"d8\" }}"));
                return dice;
            }

            foreach (JsonProperty entry in value.EnumerateObject())
            {
                string where = field + "." + entry.Name;

                if (!Vocabulary.TryWord(entry.Name, out TEnum word))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{entry.Name}' is not one of {Vocabulary.Offer<TEnum>()}"));
                    continue;
                }

                if (entry.Value.ValueKind != JsonValueKind.String ||
                    !Vocabulary.TryDie(entry.Value.GetString(), out Die die))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{Text(entry.Value)}' is not a die - it is one of {Vocabulary.Offer(Vocabulary.Dice)}"));
                    continue;
                }

                if (dice.ContainsKey(word))
                {
                    problems.Add(new ContentProblem(file, where, $"'{entry.Name}' is rated twice"));
                    continue;
                }

                dice[word] = die;
            }

            return dice;
        }

        static void Gear(JsonElement root, string file, List<ContentProblem> problems,
                         out string gearId, out Die gearDie)
        {
            gearId = null;
            gearDie = Die.None;

            if (!root.TryGetProperty("gear", out JsonElement value)) return;

            if (value.ValueKind == JsonValueKind.Null) return;

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, "gear", "gear is an object, like { \"id\": \"claw\", \"die\": \"d6\" }"));
                return;
            }

            if (!value.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.String ||
                !ContentId.IsLocal(id.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "gear.id",
                    "gear needs an id - lowercase a-z, 0-9 and underscore, and it is what names it"));
                return;
            }

            if (!value.TryGetProperty("die", out JsonElement die) || die.ValueKind != JsonValueKind.String ||
                !Vocabulary.TryDie(die.GetString(), out Die rated))
            {
                problems.Add(new ContentProblem(
                    file, "gear.die",
                    $"gear needs a die - one of {Vocabulary.Offer(Vocabulary.Dice)}"));
                return;
            }

            gearId = id.GetString();
            gearDie = rated;
        }

        // gear ids are not scoped; a campaign's claw shares the key table with the engine's gear

        static string Behaviour(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("behaviour", out JsonElement value)) return null;

            if (value.ValueKind == JsonValueKind.Null) return null;

            if (value.ValueKind != JsonValueKind.String || !Behaviours.Has(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "behaviour",
                    $"'{Text(value)}' is not a behaviour - it is one of " +
                    $"{Vocabulary.Offer(Behaviours.All)}"));
                return null;
            }

            return value.GetString();
        }

        static string Text(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Undefined => "nothing",
            _ => value.ToString(),
        };

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();

        static string Shown(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Undefined => "nothing",
            _ => value.ToString(),
        };
    }
}

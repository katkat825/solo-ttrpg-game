using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Items;
using Content.Schema;

namespace Content.Entities
{
    public static class EntityReader
    {
        // campaign scopes the id, exactly as a statblock's reader does
        public static Read<Entity> Parse(string json, string file, string campaign)
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
                return Read<Entity>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<Entity>.Bad(new ContentProblem(
                        file, "",
                        $"an entity is a JSON object and this is a {Named(root.ValueKind)}"));

                foreach (JsonProperty property in root.EnumerateObject())
                    if (Array.IndexOf(Fields, property.Name) < 0)
                        problems.Add(new ContentProblem(
                            file, property.Name,
                            $"an entity has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));

                string id = Id(root, file, problems);
                string mini = Optional(root, "mini", file, problems,
                                       "a mini id - the figure this stands as on the table");
                string monster = Optional(root, "monster", file, problems,
                                          "a monster id from this campaign's monsters/ - the " +
                                          "statblock this fights as when it is attacked");

                LootTable loot = root.TryGetProperty("loot", out JsonElement table)
                    ? LootTable.Parse(table, file, "loot", problems)
                    : LootTable.Nothing;

                IReadOnlyDictionary<Interaction, string> can = Can(root, file, problems);

                Consistent(can, monster, loot, file, problems);

                if (problems.Exists(p => p.IsAFault)) return Read<Entity>.Bad(problems);

                return Read<Entity>.Good(new Entity(
                    campaign != null ? ContentId.Scoped(campaign, id) : id,
                    mini, monster, can, loot));
            }
        }

        static readonly string[] Fields = { "id", "mini", "monster", "can", "loot" };

        static string Id(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    "an entity needs an id - lowercase a-z, 0-9 and underscore. A place stands it " +
                    "on a square by it, and every fact about it is named after it"));
                return null;
            }

            return value.GetString();
        }

        static string Optional(JsonElement root, string field, string file,
                               List<ContentProblem> problems, string saying)
        {
            if (!root.TryGetProperty(field, out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null)
                return "";

            // a mini may name another pack ('<pack>.<mini>'); the shelf resolves that, as for a monster's
            bool ok = value.ValueKind == JsonValueKind.String &&
                      (ContentId.IsLocal(value.GetString()) ||
                       (field == "mini" && ContentId.IsScoped(value.GetString())));

            if (!ok)
            {
                problems.Add(new ContentProblem(
                    file, field, $"'{Shown(value)}' is not {saying}"));
                return "";
            }

            return value.GetString();
        }

        static IReadOnlyDictionary<Interaction, string> Can(JsonElement root, string file,
                                                            List<ContentProblem> problems)
        {
            var can = new Dictionary<Interaction, string>();

            if (!root.TryGetProperty("can", out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null)
            {
                problems.Add(new ContentProblem(
                    file, "can",
                    "an entity needs to say what you may do with it - " +
                    "{ \"talk\": \"bob_greeting\" }. The engine offers exactly these verbs and no " +
                    $"others; they are {Vocabulary.Offer<Interaction>()}. An entity you may do " +
                    "nothing with is scenery, and scenery is drawn on the map"));
                return can;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, "can",
                    $"'can' is a verb and what it needs said about it - this is a " +
                    $"{Named(value.ValueKind)}. A verb that needs nothing takes null: " +
                    "{ \"attack\": null, \"search\": null }"));
                return can;
            }

            foreach (JsonProperty property in value.EnumerateObject())
            {
                string where = "can." + property.Name;

                if (!Vocabulary.TryWord(property.Name, out Interaction verb))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{property.Name}' is not something the engine knows how to do - it is " +
                        $"one of {Vocabulary.Offer<Interaction>()}. The vocabulary is closed on " +
                        "purpose: a verb outside it is engine work, not campaign data"));
                    continue;
                }

                if (can.ContainsKey(verb))
                {
                    problems.Add(new ContentProblem(
                        file, where, $"'{verb.Word()}' is listed twice"));
                    continue;
                }

                string wants = verb.Wants();

                if (wants.Length == 0)
                {
                    if (property.Value.ValueKind != JsonValueKind.Null)
                        problems.Add(new ContentProblem(
                            file, where,
                            $"'{verb.Word()}' needs nothing said about it, so it takes null - " +
                            $"what happens when you {verb.Word()} something is the engine's to " +
                            "decide, not the campaign's"));

                    can[verb] = "";
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String ||
                    !ContentId.IsLocal(property.Value.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, where, $"'{verb.Word()}' needs {wants}"));
                    continue;
                }

                can[verb] = property.Value.GetString();
            }

            if (can.Count == 0 && !problems.Exists(p => p.Where.StartsWith("can", StringComparison.Ordinal)))
                problems.Add(new ContentProblem(
                    file, "can",
                    "this entity permits nothing at all, so it is scenery - draw it on the map " +
                    "instead, or give it a verb"));

            return can;
        }

        static void Consistent(IReadOnlyDictionary<Interaction, string> can, string monster,
                               LootTable loot, string file, List<ContentProblem> problems)
        {
            if (can.ContainsKey(Interaction.Attack) && monster.Length == 0)
                problems.Add(new ContentProblem(
                    file, "monster",
                    "this may be attacked and has no statblock to be attacked as - name a " +
                    "monster from this campaign's monsters/. If it was never meant to be " +
                    "fought, take 'attack' off it and it cannot be"));

            if (!can.ContainsKey(Interaction.Attack) && monster.Length > 0)
                problems.Add(new ContentProblem(
                    file, "monster",
                    $"this fights as '{monster}' and cannot be attacked, so the statblock is " +
                    "never used - permit 'attack', or take the monster off"));

            if (can.ContainsKey(Interaction.Search) && loot.IsEmpty)
                problems.Add(new ContentProblem(
                    file, "loot",
                    "this may be searched and has nothing in it - give it loot, or take 'search' " +
                    "off it. An empty chest that says nothing is a bug report"));
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

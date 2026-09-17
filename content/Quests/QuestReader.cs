using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Content.World;

namespace Content.Quests
{
    public static class QuestReader
    {
        public static Read<Quest> Parse(string json, string file)
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
                return Read<Quest>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<Quest>.Bad(new ContentProblem(
                        file, "",
                        $"a quest is a JSON object and this is a {Named(root.ValueKind)}"));

                foreach (JsonProperty property in root.EnumerateObject())
                    if (Array.IndexOf(Fields, property.Name) < 0)
                        problems.Add(new ContentProblem(
                            file, property.Name,
                            $"a quest has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));

                string id = Id(root, file, problems);

                Requirement offered = Clause(root, "offered", file, problems) ?? Requirement.Always;
                Requirement done = Clause(root, "done", file, problems);
                Requirement failed = Clause(root, "failed", file, problems);

                if (done == null)
                {
                    problems.Add(new ContentProblem(
                        file, "done",
                        "a quest needs to say when it is done, as facts - " +
                        "\"done\": { \"when\": [ \"sacred_rock.delivered\" ], " +
                        "\"unless\": [ \"bob.dead\" ] }. There is no quest state anywhere but the " +
                        "facts, so this is the whole of what completing it means"));
                }
                else if (done.IsAlways)
                {
                    problems.Add(new ContentProblem(
                        file, "done",
                        "this quest is done the moment it is offered - 'done' needs at least one " +
                        "fact in it, or there is nothing to do"));
                }

                if (failed != null && failed.IsAlways)
                    problems.Add(new ContentProblem(
                        file, "failed",
                        "this quest has failed before it is offered - 'failed' needs at least one " +
                        "fact in it. Leave it out entirely for a quest that cannot fail"));

                if (problems.Exists(p => p.IsAFault)) return Read<Quest>.Bad(problems);

                return Read<Quest>.Good(new Quest(id, offered, done, failed));
            }
        }

        static readonly string[] Fields = { "id", "offered", "done", "failed" };

        static string Id(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    "a quest needs an id - lowercase a-z, 0-9 and underscore. Its title and its " +
                    "description are keyed by it, and so is the fact that says it was accepted"));
                return null;
            }

            return value.GetString();
        }

        // null means the author did not write this clause, which is different from writing an empty one
        static Requirement Clause(JsonElement root, string field, string file,
                                  List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null)
                return null;

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, field,
                    $"'{field}' is a pair of fact lists - " +
                    "{ \"when\": [ ... ], \"unless\": [ ... ] }"));
                return null;
            }

            foreach (JsonProperty property in value.EnumerateObject())
                if (!Requirement.Fields.Contains(property.Name, StringComparer.Ordinal))
                    problems.Add(new ContentProblem(
                        file, $"{field}.{property.Name}",
                        $"'{field}' has no '{property.Name}' - it has " +
                        $"{Vocabulary.Offer(Requirement.Fields)}"));

            return Requirement.Parse(value, file, field, problems);
        }

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();
    }
}

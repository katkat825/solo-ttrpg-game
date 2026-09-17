using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Content.World;

namespace Content.Places
{
    public static class RoadReader
    {
        public static Read<Road> Parse(string json, string file)
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
                return Read<Road>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<Road>.Bad(new ContentProblem(
                        file, "", $"a road is a JSON object and this is a {Named(root.ValueKind)}"));

                foreach (JsonProperty property in root.EnumerateObject())
                    if (Array.IndexOf(Fields, property.Name) < 0)
                        problems.Add(new ContentProblem(
                            file, property.Name,
                            $"a road has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));

                string id = Id(root, "id", "a road", file, problems);
                string from = Id(root, "from", "the place a road starts at", file, problems);
                string to = Id(root, "to", "the place a road leads to", file, problems);

                if (from != null && from == to)
                    problems.Add(new ContentProblem(
                        file, "to",
                        $"this road leaves '{from}' and arrives at '{from}' - a road joins two " +
                        "places"));

                IReadOnlyList<Happening> wayside = Wayside(root, file, problems);

                if (problems.Exists(p => p.IsAFault)) return Read<Road>.Bad(problems);

                return Read<Road>.Good(new Road(id, from, to, wayside));
            }
        }

        static readonly string[] Fields = { "id", "from", "to", "wayside" };

        static readonly string[] EntryFields =
        {
            "event", "weight", "place", "sets", "once", "when", "unless",
        };

        static string Id(JsonElement root, string field, string saying, string file,
                         List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, field,
                    $"{saying} needs an id - lowercase a-z, 0-9 and underscore"));
                return null;
            }

            return value.GetString();
        }

        static IReadOnlyList<Happening> Wayside(JsonElement root, string file,
                                                List<ContentProblem> problems)
        {
            var wayside = new List<Happening>();

            if (!root.TryGetProperty("wayside", out JsonElement value)) return wayside;

            if (value.ValueKind == JsonValueKind.Null) return wayside;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "wayside",
                    "wayside is the weighted table of what can happen on the way - " +
                    "[ { \"weight\": 6 }, { \"event\": \"a_wounded_traveller\", \"weight\": 1 } ]. " +
                    "An entry with no event is 'narrate and arrive', and it should be most of it"));
                return wayside;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"wayside[{at++}]";

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"a wayside entry is an object and this is a {Named(entry.ValueKind)}"));
                    continue;
                }

                foreach (JsonProperty property in entry.EnumerateObject())
                    if (Array.IndexOf(EntryFields, property.Name) < 0)
                        problems.Add(new ContentProblem(
                            file, $"{where}.{property.Name}",
                            $"a wayside entry has no '{property.Name}' - it has " +
                            $"{Vocabulary.Offer(EntryFields)}"));

                string id = "";

                if (entry.TryGetProperty("event", out JsonElement named) &&
                    named.ValueKind != JsonValueKind.Null)
                {
                    if (named.ValueKind != JsonValueKind.String ||
                        !ContentId.IsLocal(named.GetString()))
                    {
                        problems.Add(new ContentProblem(
                            file, $"{where}.event",
                            "an event id is lowercase a-z, 0-9 and underscore - leave it out " +
                            "entirely for 'nothing happened, and you arrive'"));
                        continue;
                    }

                    id = named.GetString();
                }

                int weight = 1;

                if (entry.TryGetProperty("weight", out JsonElement weighed))
                {
                    if (weighed.ValueKind != JsonValueKind.Number ||
                        !weighed.TryGetInt32(out weight) || weight < 1)
                    {
                        problems.Add(new ContentProblem(
                            file, $"{where}.weight",
                            "a weight is a whole number of 1 or more - it is how many parts in " +
                            "the table this entry is"));
                        continue;
                    }
                }

                string place = "";

                if (entry.TryGetProperty("place", out JsonElement stop) &&
                    stop.ValueKind != JsonValueKind.Null)
                {
                    if (stop.ValueKind != JsonValueKind.String || !ContentId.IsLocal(stop.GetString()))
                    {
                        problems.Add(new ContentProblem(
                            file, $"{where}.place",
                            "a place is the id of a place in this campaign - travel stops and the " +
                            "GM lays it out. Leave it out for something that happens without one"));
                        continue;
                    }

                    place = stop.GetString();
                }

                string sets = "";

                if (entry.TryGetProperty("sets", out JsonElement fact) &&
                    fact.ValueKind != JsonValueKind.Null)
                {
                    string explained = fact.ValueKind == JsonValueKind.String
                        ? FactName.Explain(fact.GetString())
                        : "a fact is a name";

                    if (explained != FactName.WellFormed)
                    {
                        problems.Add(new ContentProblem(file, $"{where}.sets", explained));
                        continue;
                    }

                    sets = fact.GetString();
                }

                bool once = false;

                if (entry.TryGetProperty("once", out JsonElement only) &&
                    only.ValueKind != JsonValueKind.Null)
                {
                    if (only.ValueKind != JsonValueKind.True && only.ValueKind != JsonValueKind.False)
                    {
                        problems.Add(new ContentProblem(
                            file, $"{where}.once",
                            "once is true or false - true is a hand-authored event that fires one " +
                            "time and then leaves the table"));
                        continue;
                    }

                    once = only.ValueKind == JsonValueKind.True;
                }

                if (once && id.Length == 0)
                {
                    problems.Add(new ContentProblem(
                        file, $"{where}.once",
                        "'nothing happened' cannot be a one-shot - there would be no event to " +
                        "remember having happened"));
                    continue;
                }

                Requirement needs = Requirement.Parse(entry, file, where, problems);

                wayside.Add(new Happening(id, weight, place, sets, once, needs));
            }

            return wayside;
        }

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();
    }
}

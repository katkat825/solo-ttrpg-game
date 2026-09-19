using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;

namespace Content.Companions
{
    public static class CompanionReader
    {
        public static Read<CompanionCard> Parse(string json, string file, string pack)
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
                return Read<CompanionCard>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<CompanionCard>.Bad(new ContentProblem(
                        file, "", $"a companion is a JSON object and this is a {Named(root.ValueKind)}"));

                foreach (JsonProperty property in root.EnumerateObject())
                    if (Array.IndexOf(Fields, property.Name) < 0)
                        problems.Add(new ContentProblem(
                            file, property.Name,
                            $"a companion has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}. " +
                            "It has no Vigor and no Defense either: it is never in the fight, and " +
                            "a card with numbers on it is one somebody will eventually give a turn"));

                string local = Id(root, file, problems);
                Perch perch = Where(root, file, problems);
                string voice = Voice(root, local, file, problems);
                string mini = Mini(root, file, problems);
                int idles = Idles(root, file, problems);
                float size = Size(root, file, problems);

                if (problems.Count > 0) return Read<CompanionCard>.Bad(problems);

                string id = ContentId.IsCampaign(pack) ? ContentId.Scoped(pack, local) : local;

                return Read<CompanionCard>.Good(
                    new CompanionCard(id, perch, voice, mini, idles, size));
            }
        }

        static readonly string[] Fields = { "id", "perch", "voice", "mini", "idles", "size" };

        static string Id(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    "a companion needs an id - lowercase a-z, 0-9 and underscore. A class names " +
                    "it to say which creature comes with it"));
                return null;
            }

            return value.GetString();
        }

        static Perch Where(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("perch", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String ||
                !Perches.TryWord(value.GetString(), out Perch perch))
            {
                problems.Add(new ContentProblem(
                    file, "perch",
                    $"'{Shown(value)}' is not somewhere on this table - it is one of " +
                    $"{Vocabulary.Offer(Perches.Words)}. Every one of them is on the table and " +
                    "none is on the map, which is the point"));
                return Perch.MapEdge;
            }

            return perch;
        }

        // unscoped on purpose: a voice is the engine's shared vocabulary, like actor.rabble.*
        static string Voice(JsonElement root, string id, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("voice", out JsonElement value)) return id;

            if (value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "voice",
                    $"'{Shown(value)}' is not a creature - a voice is keyed by what the thing is " +
                    "('wolf'), never by the class that brought it ('barbarian'). Leave it out and " +
                    "the companion speaks under its own id"));
                return id;
            }

            return value.GetString();
        }

        static string Mini(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("mini", out JsonElement value)) return "";

            if (value.ValueKind != JsonValueKind.String ||
                (!ContentId.IsLocal(value.GetString()) && !ContentId.IsScoped(value.GetString())))
            {
                problems.Add(new ContentProblem(
                    file, "mini",
                    $"'{Shown(value)}' is not a mini id - leave it out for the placeholder shape, " +
                    "which is what W1 asks for anyway: build the idles first"));
                return "";
            }

            return value.GetString();
        }

        static int Idles(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("idles", out JsonElement value)) return CompanionCard.IdlesByDefault;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int idles) ||
                idles < 1 || idles > CompanionCard.MostIdles)
            {
                problems.Add(new ContentProblem(
                    file, "idles",
                    $"'{Shown(value)}' is not a number of idles - 1 to {CompanionCard.MostIdles}. " +
                    "About a dozen is what reads as alive; one is a statue that twitches"));
                return CompanionCard.IdlesByDefault;
            }

            return idles;
        }

        // HOW BIG IT IS. Left out, it is wolf-sized, because that is the creature the table was
        // built around and every campaign that shipped before this field said nothing.
        static float Size(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("size", out JsonElement value)) return CompanionCard.NormalSize;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out float size) ||
                size < CompanionCard.Smallest || size > CompanionCard.Largest)
            {
                problems.Add(new ContentProblem(
                    file, "size",
                    $"'{Shown(value)}' is not a size - {CompanionCard.Smallest} to " +
                    $"{CompanionCard.Largest}, where 1 is the house wolf. A raven is about a third " +
                    "of it and a reliquary about a fifth. It changes how big the thing beside the " +
                    "table looks and nothing else: there are no numbers on this card"));
                return CompanionCard.NormalSize;
            }

            return size;
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

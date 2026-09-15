using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Core.Space;

namespace Content.Encounters
{
    public static class EncounterReader
    {
        public static Read<EncounterPlan> Parse(string json, string file)
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
                return Read<EncounterPlan>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<EncounterPlan>.Bad(new ContentProblem(
                        file, "",
                        $"an encounter is a JSON object and this is a {Named(root.ValueKind)}"));

                string id = Id(root, file, problems);
                string map = Map(root, file, problems);
                IReadOnlyList<Placement> placements = Placements(root, file, problems);
                IReadOnlyList<Trigger> triggers = Triggers(root, file, problems);
                IReadOnlyList<Cue> cues = Cues(root, file, problems);

                Unknown(root, file, problems);

                if (problems.Count > 0) return Read<EncounterPlan>.Bad(problems);

                return Read<EncounterPlan>.Good(
                    new EncounterPlan(id, map, placements, triggers, cues));
            }
        }

        static readonly string[] Fields = { "id", "map", "placements", "triggers", "cues" };

        static void Unknown(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(Fields, property.Name) >= 0) continue;

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"an encounter has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));
            }
        }

        static string Id(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    "an encounter needs an id - lowercase a-z, 0-9 and underscore. A chapter " +
                    "names its encounters by it"));
                return null;
            }

            return value.GetString();
        }

        static string Map(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("map", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "map",
                    "an encounter needs the room it is fought in - the name of a file in this " +
                    "campaign's maps/, without the .map"));
                return null;
            }

            return value.GetString();
        }

        static IReadOnlyList<Placement> Placements(JsonElement root, string file,
                                                   List<ContentProblem> problems)
        {
            var placements = new List<Placement>();

            if (!root.TryGetProperty("placements", out JsonElement value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "placements",
                    "an encounter needs its placements - " +
                    "[ { \"slot\": 1, \"monster\": \"cinder_hound\" } ]"));
                return placements;
            }

            if (value.GetArrayLength() == 0)
                problems.Add(new ContentProblem(
                    file, "placements",
                    "this encounter places nobody, so there is nothing to fight - put somebody on " +
                    "a spawn slot, or delete the file"));

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"placements[{at++}]";

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"a placement is an object and this is a {Named(entry.ValueKind)}"));
                    continue;
                }

                Placement placement = One(entry, file, where, problems);

                if (placement == null) continue;

                Placement standing = placements.Find(p => p.Slot == placement.Slot);

                if (standing != null)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"spawn {placement.Slot} already has '{standing.Monster}' on it - one " +
                        "piece per square"));
                    continue;
                }

                placements.Add(placement);
            }

            return placements;
        }

        static readonly string[] PlacementFields = { "slot", "monster" };

        static Placement One(JsonElement entry, string file, string where,
                             List<ContentProblem> problems)
        {
            foreach (JsonProperty property in entry.EnumerateObject())
                if (Array.IndexOf(PlacementFields, property.Name) < 0)
                    problems.Add(new ContentProblem(
                        file, $"{where}.{property.Name}",
                        $"a placement has no '{property.Name}' - it has " +
                        $"{Vocabulary.Offer(PlacementFields)}"));

            bool good = true;

            if (!entry.TryGetProperty("slot", out JsonElement slotValue) ||
                slotValue.ValueKind != JsonValueKind.Number || !slotValue.TryGetInt32(out int slot))
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.slot",
                    $"a placement needs the spawn slot it stands on, as a whole number " +
                    $"{FirstSlot} to {LastSlot} - the digit drawn on the map"));
                return null;
            }

            // the range is derived from MapReader's own glyph constants, so it can't drift from them
            if (slot < FirstSlot || slot > LastSlot)
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.slot",
                    $"{slot} is not a spawn slot - a map draws {FirstSlot} to {LastSlot}, because " +
                    "a slot is one digit on the grid"));
                good = false;
            }

            if (!entry.TryGetProperty("monster", out JsonElement idValue) ||
                idValue.ValueKind != JsonValueKind.String || !ContentId.IsLocal(idValue.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.monster",
                    "a placement needs a monster, by the id in this campaign's monsters/ - " +
                    "'ghoul', not 'ashfall.ghoul', because the campaign it is in is already known"));
                return null;
            }

            return good ? new Placement(slot, idValue.GetString()) : null;
        }

        public static int FirstSlot => MapReader.FirstSpawnGlyph - '0';

        public static int LastSlot => MapReader.LastSpawnGlyph - '0';

        static readonly string[] TriggerFields = { "when", "then", "encounter" };

        static IReadOnlyList<Trigger> Triggers(JsonElement root, string file,
                                               List<ContentProblem> problems)
        {
            var triggers = new List<Trigger>();

            if (!root.TryGetProperty("triggers", out JsonElement value)) return triggers;

            if (value.ValueKind == JsonValueKind.Null) return triggers;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "triggers",
                    "triggers is a list - [ { \"when\": \"cleared\", \"then\": \"next\" } ]"));
                return triggers;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"triggers[{at++}]";

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where, $"a trigger is an object and this is a {Named(entry.ValueKind)}"));
                    continue;
                }

                foreach (JsonProperty property in entry.EnumerateObject())
                    if (Array.IndexOf(TriggerFields, property.Name) < 0)
                        problems.Add(new ContentProblem(
                            file, $"{where}.{property.Name}",
                            $"a trigger has no '{property.Name}' - it has " +
                            $"{Vocabulary.Offer(TriggerFields)}"));

                if (!Word(entry, "when", file, where, problems, out When when)) continue;
                if (!Word(entry, "then", file, where, problems, out Then then)) continue;

                string encounter = "";

                if (then == Then.Goto)
                {
                    if (!entry.TryGetProperty("encounter", out JsonElement target) ||
                        target.ValueKind != JsonValueKind.String ||
                        !ContentId.IsLocal(target.GetString()))
                    {
                        problems.Add(new ContentProblem(
                            file, $"{where}.encounter",
                            "'goto' needs to say where - name an encounter in this campaign"));
                        continue;
                    }

                    encounter = target.GetString();
                }
                else if (entry.TryGetProperty("encounter", out JsonElement extra) &&
                         extra.ValueKind != JsonValueKind.Null)
                {
                    // refused, not ignored: an encounter on a non-goto trigger would silently do nothing
                    problems.Add(new ContentProblem(
                        file, $"{where}.encounter",
                        $"only 'goto' goes to a named encounter - '{Vocabulary.NameOf(then)}' " +
                        "would ignore this"));
                    continue;
                }

                triggers.Add(new Trigger(when, then, encounter));
            }

            return triggers;
        }

        static readonly string[] CueFields = { "when", "cue", "gesture", "hesitant", "at" };

        static IReadOnlyList<Cue> Cues(JsonElement root, string file, List<ContentProblem> problems)
        {
            var cues = new List<Cue>();

            if (!root.TryGetProperty("cues", out JsonElement value)) return cues;

            if (value.ValueKind == JsonValueKind.Null) return cues;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "cues",
                    "cues is a list - [ { \"when\": \"entered\", \"cue\": \"the_yard_opens\" } ]"));
                return cues;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"cues[{at++}]";

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where, $"a cue is an object and this is a {Named(entry.ValueKind)}"));
                    continue;
                }

                foreach (JsonProperty property in entry.EnumerateObject())
                    if (Array.IndexOf(CueFields, property.Name) < 0)
                        problems.Add(new ContentProblem(
                            file, $"{where}.{property.Name}",
                            $"a cue has no '{property.Name}' - it has {Vocabulary.Offer(CueFields)}"));

                if (!Word(entry, "when", file, where, problems, out When when)) continue;

                if (!entry.TryGetProperty("cue", out JsonElement id) ||
                    id.ValueKind != JsonValueKind.String || !ContentId.IsLocal(id.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, $"{where}.cue",
                        "a cue needs a name - lowercase a-z, 0-9 and underscore. What the DM does " +
                        "with it is Phase D's; the name is so the campaign need not be re-authored " +
                        "to gain it"));
                    continue;
                }

                Gesture gesture = Does(entry, file, where, problems);

                bool hesitant = Hesitant(entry, gesture, file, where, problems);

                int slot = At(entry, gesture, file, where, problems);

                cues.Add(new Cue(when, id.GetString(), gesture, hesitant, slot));
            }

            return cues;
        }

        // the gesture vocabulary is closed; an unknown one is refused here, not mid-fight
        static Gesture Does(JsonElement entry, string file, string where,
                            List<ContentProblem> problems)
        {
            if (!entry.TryGetProperty("gesture", out JsonElement value)) return Gesture.Push;

            if (value.ValueKind == JsonValueKind.String &&
                Vocabulary.TryWord(value.GetString(), out Gesture gesture))
                return gesture;

            problems.Add(new ContentProblem(
                file, $"{where}.gesture",
                $"'{Shown(value)}' is not something the DM's hands can do - it is one of " +
                $"{Vocabulary.Offer<Gesture>()}. The vocabulary is the engine's and the timing is " +
                "yours; a gesture outside that list is animation work, " +
                "not campaign data"));

            return Gesture.Push;
        }

        static bool Hesitant(JsonElement entry, Gesture gesture, string file, string where,
                             List<ContentProblem> problems)
        {
            if (!entry.TryGetProperty("hesitant", out JsonElement value)) return false;

            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.hesitant",
                    $"'{Shown(value)}' is not true or false - hesitant is the pause before a mini " +
                    "goes down"));
                return false;
            }

            bool hesitant = value.ValueKind == JsonValueKind.True;

            if (hesitant && !gesture.CanHesitate())
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.hesitant",
                    $"a {gesture.Word()} cannot hesitate - the pause is the variant of a " +
                    $"'{Gesture.Place.Word()}' - a whole story in " +
                    "half a second - and it means nothing anywhere else"));
                return false;
            }

            return hesitant;
        }

        static int At(JsonElement entry, Gesture gesture, string file, string where,
                      List<ContentProblem> problems)
        {
            if (!entry.TryGetProperty("at", out JsonElement value)) return 0;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int slot) ||
                slot < 1)
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.at",
                    $"'{Shown(value)}' is not a spawn slot - a whole number from 1, drawn on the " +
                    "map this encounter names"));
                return 0;
            }

            if (!gesture.NeedsASquare())
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.at",
                    $"a {gesture.Word()} does not happen at a square - it happens at the table or " +
                    "behind the screen, so there is nowhere for a slot to be"));
                return 0;
            }

            return slot;
        }

        static bool Word<TEnum>(JsonElement entry, string field, string file, string where,
                                List<ContentProblem> problems, out TEnum word)
            where TEnum : struct, Enum
        {
            word = default;

            if (!entry.TryGetProperty(field, out JsonElement value) ||
                value.ValueKind != JsonValueKind.String ||
                !Vocabulary.TryWord(value.GetString(), out word))
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.{field}",
                    $"'{Shown(value)}' is not a '{field}' - it is one of {Vocabulary.Offer<TEnum>()}"));
                return false;
            }

            return true;
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

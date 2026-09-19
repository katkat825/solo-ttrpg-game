using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Content.World;
using Core.Space;

namespace Content.Places
{
    public static class PlaceReader
    {
        public static Read<Place> Parse(string json, string file)
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
                return Read<Place>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message, (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<Place>.Bad(new ContentProblem(
                        file, "",
                        $"a place is a JSON object and this is a {Named(root.ValueKind)}"));

                string id = Id(root, file, problems);
                string map = Map(root, file, problems);
                IReadOnlyList<Standing> standing = Standings(root, file, problems);
                IReadOnlyList<Exit> exits = Exits(root, file, problems);
                IReadOnlyList<Trigger> triggers = Triggers(root, file, problems);
                IReadOnlyList<Cue> cues = Cues(root, file, problems);
                IReadOnlyDictionary<Content.Sheet.Check, int> checks = Checks(root, file, problems);

                Unknown(root, file, problems);

                if (standing.Count == 0 && exits.Count == 0 && triggers.Count == 0 &&
                    checks.Count == 0)
                    problems.Add(new ContentProblem(
                        file, "",
                        "nobody stands in this place, there is no way out of it and nothing " +
                        "happens in it - put somebody on a spawn slot, or an exit, or a trigger, " +
                        "or delete the file"));

                if (problems.Exists(p => p.IsAFault)) return Read<Place>.Bad(problems);

                return Read<Place>.Good(
                    new Place(id, map, standing, exits, triggers, cues, checks));
            }
        }

        // 'placements' is format 1's spelling of 'standing', read while ContentFormat.Oldest is 1
        public const string Placements = "placements";

        static readonly string[] Fields =
        {
            "id", "map", "standing", Placements, "exits", "triggers", "cues", "checks",
        };

        // WHAT YOU MAY TRY HERE, OFF YOUR OWN SHEET, AND HOW HARD THE SCENE MAKES IT.
        //
        // { "checks": { "persuade": 11, "perception": 9 } } and nothing more. A check left out is
        // one this place does not offer, which is the author saying there is nobody here to try
        // it on - and a check listed is the kit's own check primitive against that number, so
        // allowing one is a line of data and never a line of engine.
        static IReadOnlyDictionary<Content.Sheet.Check, int> Checks(
            JsonElement root, string file, List<ContentProblem> problems)
        {
            var allowed = new Dictionary<Content.Sheet.Check, int>();

            if (!root.TryGetProperty("checks", out JsonElement value)) return allowed;

            if (value.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, "checks",
                    $"'checks' is a {Named(value.ValueKind)} and it is an object of " +
                    $"{Vocabulary.Offer(Content.Sheet.Checks.Words)} against a difficulty - " +
                    @"{ ""persuade"": 11 }"));
                return allowed;
            }

            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!Content.Sheet.Checks.TryWord(property.Name, out Content.Sheet.Check check))
                {
                    problems.Add(new ContentProblem(
                        file, "checks." + property.Name,
                        $"'{property.Name}' is not something you can try - it is one of " +
                        $"{Vocabulary.Offer(Content.Sheet.Checks.Words)}. They are the three a " +
                        "character reaches for off their own sheet; a fourth is engine work, the " +
                        "same as a sixth verb is"));
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Number ||
                    !property.Value.TryGetInt32(out int against) ||
                    against < Core.Resolution.Difficulty.Easy ||
                    against > Core.Resolution.Difficulty.Legendary)
                {
                    problems.Add(new ContentProblem(
                        file, "checks." + property.Name,
                        $"'{Shown(property.Value)}' is not a difficulty - " +
                        $"{Core.Resolution.Difficulty.Easy} to " +
                        $"{Core.Resolution.Difficulty.Legendary}, where " +
                        $"{Core.Resolution.Difficulty.Standard} is standard and " +
                        $"{Core.Resolution.Difficulty.Formidable} is a gate a starting hero " +
                        "cannot pass"));
                    continue;
                }

                if (allowed.ContainsKey(check))
                {
                    problems.Add(new ContentProblem(
                        file, "checks." + property.Name,
                        $"'{property.Name}' is allowed twice here, at two difficulties - one of " +
                        "the two would never be the one used"));
                    continue;
                }

                allowed[check] = against;
            }

            return allowed;
        }

        static void Unknown(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(Fields, property.Name) >= 0) continue;

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"a place has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));
            }
        }

        static string Id(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    "a place needs an id - lowercase a-z, 0-9 and underscore. A chapter names " +
                    "its places by it, and so does every exit that leads here"));
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
                    "a place needs the layout it is played on - the name of a file in this " +
                    "campaign's maps/, without the .map"));
                return null;
            }

            return value.GetString();
        }


        static IReadOnlyList<Standing> Standings(JsonElement root, string file,
                                                 List<ContentProblem> problems)
        {
            var standing = new List<Standing>();

            string field = root.TryGetProperty("standing", out JsonElement value) ? "standing"
                         : root.TryGetProperty(Placements, out value) ? Placements
                         : null;

            if (field == null) return standing;

            if (value.ValueKind == JsonValueKind.Null) return standing;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, field,
                    "standing is a list of who and what is on the map - " +
                    "[ { \"slot\": 1, \"monster\": \"cinder_hound\" }, " +
                    "{ \"slot\": 2, \"entity\": \"bob\" } ]"));
                return standing;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"{field}[{at++}]";

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"a standing is an object and this is a {Named(entry.ValueKind)}"));
                    continue;
                }

                Standing one = OneStanding(entry, file, where, problems);

                if (one == null) continue;

                Standing already = standing.Find(s => s.Slot == one.Slot);

                if (already != null)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"spawn {one.Slot} already has '{already.Id}' on it - one piece per square"));
                    continue;
                }

                standing.Add(one);
            }

            return standing;
        }

        static readonly string[] StandingFields =
        {
            "slot", "monster", "entity", "when", "unless",
        };

        static Standing OneStanding(JsonElement entry, string file, string where,
                                    List<ContentProblem> problems)
        {
            foreach (JsonProperty property in entry.EnumerateObject())
                if (Array.IndexOf(StandingFields, property.Name) < 0)
                    problems.Add(new ContentProblem(
                        file, $"{where}.{property.Name}",
                        $"a standing has no '{property.Name}' - it has " +
                        $"{Vocabulary.Offer(StandingFields)}"));

            bool good = Slot(entry, "slot", file, where, problems, out int slot);

            bool isFoe = entry.TryGetProperty("monster", out JsonElement monster) &&
                         monster.ValueKind != JsonValueKind.Null;

            bool isThing = entry.TryGetProperty("entity", out JsonElement thing) &&
                           thing.ValueKind != JsonValueKind.Null;

            if (isFoe && isThing)
            {
                problems.Add(new ContentProblem(
                    file, where,
                    "this square has both a monster and an entity on it - a monster is something " +
                    "to fight and an entity is something to deal with, and one square holds one " +
                    "of them"));
                return null;
            }

            if (!isFoe && !isThing)
            {
                problems.Add(new ContentProblem(
                    file, where,
                    "a standing needs a monster or an entity - a monster by the id in this " +
                    "campaign's monsters/, an entity by the id in its entities/"));
                return null;
            }

            JsonElement named = isFoe ? monster : thing;
            string what = isFoe ? "monster" : "entity";

            if (named.ValueKind != JsonValueKind.String || !ContentId.IsLocal(named.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.{what}",
                    $"a standing needs a {what}, by the id in this campaign's {what}s/ folder - " +
                    $"'ghoul', not 'ashfall.ghoul', because the campaign it is in is already known"));
                return null;
            }

            Requirement needs = Requirement.Parse(entry, file, where, problems);

            if (!good) return null;

            return isFoe
                ? Standing.Foe(slot, named.GetString(), needs)
                : Standing.Thing(slot, named.GetString(), needs);
        }

        // the range is derived from MapReader's own glyph constants, so it can't drift from them
        static bool Slot(JsonElement entry, string field, string file, string where,
                         List<ContentProblem> problems, out int slot)
        {
            slot = 0;

            if (!entry.TryGetProperty(field, out JsonElement value) ||
                value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out slot))
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.{field}",
                    $"this needs the spawn slot it stands on, as a whole number {FirstSlot} to " +
                    $"{LastSlot} - the digit drawn on the map"));
                return false;
            }

            if (slot < FirstSlot || slot > LastSlot)
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.{field}",
                    $"{slot} is not a spawn slot - a map draws {FirstSlot} to {LastSlot}, because " +
                    "a slot is one digit on the grid"));
                return false;
            }

            return true;
        }

        public static int FirstSlot => MapReader.FirstSpawnGlyph - '0';

        public static int LastSlot => MapReader.LastSpawnGlyph - '0';


        static readonly string[] ExitFields =
        {
            "slot", "to", "arriving", "road", "when", "unless",
        };

        static IReadOnlyList<Exit> Exits(JsonElement root, string file,
                                         List<ContentProblem> problems)
        {
            var exits = new List<Exit>();

            if (!root.TryGetProperty("exits", out JsonElement value)) return exits;

            if (value.ValueKind == JsonValueKind.Null) return exits;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "exits",
                    "exits is a list of the ways out - " +
                    "[ { \"slot\": 5, \"to\": \"the_cellar\", \"arriving\": 6 } ]"));
                return exits;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"exits[{at++}]";

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where, $"an exit is an object and this is a {Named(entry.ValueKind)}"));
                    continue;
                }

                foreach (JsonProperty property in entry.EnumerateObject())
                    if (Array.IndexOf(ExitFields, property.Name) < 0)
                        problems.Add(new ContentProblem(
                            file, $"{where}.{property.Name}",
                            $"an exit has no '{property.Name}' - it has " +
                            $"{Vocabulary.Offer(ExitFields)}"));

                bool good = Slot(entry, "slot", file, where, problems, out int slot);

                if (!entry.TryGetProperty("to", out JsonElement to) ||
                    to.ValueKind != JsonValueKind.String || !ContentId.IsLocal(to.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, $"{where}.to",
                        "an exit needs somewhere to lead - the id of a place in this campaign"));
                    continue;
                }

                int arriving = 0;

                if (entry.TryGetProperty("arriving", out JsonElement landing) &&
                    landing.ValueKind != JsonValueKind.Null &&
                    !Slot(entry, "arriving", file, where, problems, out arriving))
                    good = false;

                string road = Local(entry, "road", file, where, problems,
                                    "a road id, naming a file in this campaign's roads/ - leave " +
                                    "it out for a step through a door", out bool badRoad);

                if (badRoad) good = false;

                Requirement needs = Requirement.Parse(entry, file, where, problems);

                Exit already = exits.Find(e => e.Slot == slot);

                if (already != null)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"spawn {slot} is already the way to '{already.To}' - one way out per " +
                        "square"));
                    continue;
                }

                if (good) exits.Add(new Exit(slot, to.GetString(), arriving, road, needs));
            }

            return exits;
        }


        static readonly string[] TriggerFields =
        {
            "when", "fact", "then", "place", "encounter", "sets",
        };

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

                if (!Watched(entry, when, file, where, problems, out string fact)) continue;

                string place = "";

                if (then.NeedsAPlace())
                {
                    // 'encounter' is format 1's spelling, read for the same reason as 'placements'
                    if (!Target(entry, "place", file, where, problems, out place) &&
                        !Target(entry, "encounter", file, where, problems, out place))
                    {
                        problems.Add(new ContentProblem(
                            file, $"{where}.place",
                            "'goto' needs to say where - name a place in this campaign"));
                        continue;
                    }
                }
                else if (Named(entry, "place") || Named(entry, "encounter"))
                {
                    // refused, not ignored: a place on a non-goto trigger would silently do nothing
                    problems.Add(new ContentProblem(
                        file, $"{where}.place",
                        $"only 'goto' goes to a named place - '{then.Word()}' would ignore this"));
                    continue;
                }

                string sets = "";

                if (then.NeedsAFact())
                {
                    if (!Fact(entry, "sets", file, where, problems, out sets))
                    {
                        problems.Add(new ContentProblem(
                            file, $"{where}.sets",
                            $"'{then.Word()}' needs the fact it writes - 'the_cellar.open', and " +
                            "whatever else in this campaign reads that fact will see it"));
                        continue;
                    }
                }
                else if (Named(entry, "sets"))
                {
                    problems.Add(new ContentProblem(
                        file, $"{where}.sets",
                        $"only 'set' and 'clear' write a fact - '{then.Word()}' would ignore this"));
                    continue;
                }

                triggers.Add(new Trigger(when, then, place, fact, sets));
            }

            return triggers;
        }


        static readonly string[] CueFields =
        {
            "when", "fact", "cue", "gesture", "hesitant", "at", "beat",
        };

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

                if (!Watched(entry, when, file, where, problems, out string fact)) continue;

                if (!entry.TryGetProperty("cue", out JsonElement id) ||
                    id.ValueKind != JsonValueKind.String || !ContentId.IsLocal(id.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, $"{where}.cue",
                        "a cue needs a name - lowercase a-z, 0-9 and underscore. It is the id of " +
                        "the line the DM reads, and the campaign's locale file gives it words"));
                    continue;
                }

                Gesture gesture = Does(entry, file, where, problems);

                bool hesitant = Hesitant(entry, gesture, file, where, problems);

                int slot = At(entry, gesture, file, where, problems);

                string beat = Beat(entry, file, where, problems);

                cues.Add(new Cue(when, id.GetString(), gesture, hesitant, slot, beat, fact));
            }

            return cues;
        }

        // exactly one moment needs a fact beside it, and it is refused both ways round
        static bool Watched(JsonElement entry, When when, string file, string where,
                            List<ContentProblem> problems, out string fact)
        {
            fact = "";

            bool named = Fact(entry, "fact", file, where, problems, out fact);

            if (when.NeedsAFact() && !named)
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.fact",
                    "'fact' is the moment a thing becomes true, so it needs the fact - " +
                    "\"fact\": \"bob.dead\". The engine remembers the death; what it means here " +
                    "is yours"));
                return false;
            }

            if (!when.NeedsAFact() && Named(entry, "fact"))
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.fact",
                    $"only the 'fact' moment waits on a fact - '{when.Word()}' would ignore this"));
                return false;
            }

            return true;
        }

        static bool Fact(JsonElement entry, string field, string file, string where,
                         List<ContentProblem> problems, out string fact)
        {
            fact = "";

            if (!Named(entry, field)) return false;

            entry.TryGetProperty(field, out JsonElement value);

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.{field}",
                    $"a fact is a name and this is a {Named(value.ValueKind)}"));
                return false;
            }

            string explained = FactName.Explain(value.GetString());

            if (explained != FactName.WellFormed)
            {
                problems.Add(new ContentProblem(file, $"{where}.{field}", explained));
                return false;
            }

            fact = value.GetString();
            return true;
        }

        static bool Target(JsonElement entry, string field, string file, string where,
                           List<ContentProblem> problems, out string id)
        {
            id = "";

            if (!entry.TryGetProperty(field, out JsonElement value) ||
                value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
                return false;

            id = value.GetString();
            return true;
        }

        static string Local(JsonElement entry, string field, string file, string where,
                            List<ContentProblem> problems, string saying, out bool bad)
        {
            bad = false;

            if (!Named(entry, field)) return "";

            entry.TryGetProperty(field, out JsonElement value);

            if (value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(file, $"{where}.{field}",
                                                $"'{Shown(value)}' is not {saying}"));
                bad = true;
                return "";
            }

            return value.GetString();
        }

        static bool Named(JsonElement entry, string field) =>
            entry.TryGetProperty(field, out JsonElement value) &&
            value.ValueKind != JsonValueKind.Null;


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
                "yours; a gesture outside that list is animation work, not campaign data"));

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
                    $"'{Gesture.Place.Word()}' - a whole story in half a second - and it means " +
                    "nothing anywhere else"));
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
                    "map this place names"));
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

        static string Beat(JsonElement entry, string file, string where,
                           List<ContentProblem> problems)
        {
            if (!entry.TryGetProperty("beat", out JsonElement value)) return "";

            if (value.ValueKind == JsonValueKind.Null) return "";

            if (value.ValueKind != JsonValueKind.String || !ContentId.IsLocal(value.GetString()))
            {
                problems.Add(new ContentProblem(
                    file, $"{where}.beat",
                    $"'{Shown(value)}' is not a beat id - it names a beat in this campaign's " +
                    "beats/ folder, and every companion at the table has its own phrasing of it"));
                return "";
            }

            return value.GetString();
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

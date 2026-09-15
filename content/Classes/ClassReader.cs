using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Core.Characters;
using Core.Dice;

namespace Content.Classes
{
    public static class ClassReader
    {
        // an empty pack (tests, an engine-shipped class) is read un-prefixed, like the built-in roster
        public static Read<ClassCard> Parse(string json, string file, string pack)
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
                return Read<ClassCard>.Bad(new ContentProblem(
                    file, "", "this is not JSON - " + bad.Message,
                    (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<ClassCard>.Bad(new ContentProblem(
                        file, "", $"a class is a JSON object and this is a {Named(root.ValueKind)}"));

                string id = Id(root, file, pack, problems);

                int vigor = Count(root, "vigor", 1, file, problems);
                int defense = Count(root, "defense", 1, file, problems);

                IReadOnlyDictionary<Attr, Die> attributes = Dice<Attr>(root, "attributes", file, problems);
                IReadOnlyDictionary<Skill, Die> skills = Dice<Skill>(root, "skills", file, problems);

                // a hero with no attributes throws no pool; the symptom otherwise is an empty tray and no clue why
                if (attributes.Count == 0 && !root.TryGetProperty("attributes", out _))
                    problems.Add(new ContentProblem(
                        file, "attributes",
                        "a class needs attributes - it is the hero, and a hero with none throws " +
                        "no pool at all. It is an object of name to die, like { \"might\": \"d8\" }"));

                string wields = Item(root, "wields", file, problems);
                string wears = Item(root, "wears", file, problems);

                IReadOnlyList<string> kit = Kit(root, file, problems);
                string mini = Mini(root, file, problems);
                int nerve = Nerve(root, file, problems);
                IReadOnlyList<Growth> growth = Steps(root, file, problems);

                Refused(root, file, problems);
                Unknown(root, file, problems);

                if (problems.Count > 0) return Read<ClassCard>.Bad(problems);

                return Read<ClassCard>.Good(new ClassCard(
                    id, vigor, defense, attributes, skills, wields, wears, kit, mini, nerve,
                    growth));
            }
        }

        // a field not listed here is a typo; silently ignoring it is a class quietly not what its author wrote
        static readonly string[] Fields =
        {
            "id", "vigor", "defense", "attributes", "skills", "wields", "wears", "kit", "mini",
            "nerve", "growth",
        };

        // caught here, not as typos: writing "tier" on a class is an assumption, not a misspelling
        static readonly (string Field, string Why)[] NotOnAHero =
        {
            ("tier", "a class is always the hero. Tier is a foe's word - how much of a fight " +
                     "something is - and the hero's has been the same one since the game had a " +
                     "hero. Vigor and defense are the numbers that make a class tough"),

            ("behaviour", "a behaviour picks which foe a creature swings at, and the hero is " +
                          "picked for by the player. What a class can DO goes in 'kit'"),

            ("loot", "loot is what is taken off a body, and nobody loots the hero. A class's " +
                     "starting gear is 'wields' and 'wears'"),
        };

        static void Refused(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach ((string field, string why) in NotOnAHero)
                if (root.TryGetProperty(field, out _))
                    problems.Add(new ContentProblem(file, field, $"a class has no '{field}' - {why}"));
        }

        static void Unknown(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (Array.IndexOf(Fields, property.Name) >= 0) continue;

                // already refused above, with a better sentence
                if (Refused(property.Name)) continue;

                if (property.Name == "name" || property.Name == "description")
                {
                    problems.Add(new ContentProblem(
                        file, property.Name,
                        $"a class's {property.Name} is not written in the file - it is a key, so " +
                        "it can be translated. Put it in this pack's locale/ CSV as " +
                        $"'{KeyLine(root, property.Name)}'"));
                    continue;
                }

                problems.Add(new ContentProblem(
                    file, property.Name,
                    $"a class has no '{property.Name}' - it has {Vocabulary.Offer(Fields)}"));
            }
        }

        static bool Refused(string field)
        {
            foreach ((string name, string _) in NotOnAHero)
                if (name == field) return true;

            return false;
        }

        static string KeyLine(JsonElement root, string aspect)
        {
            string id = root.TryGetProperty("id", out JsonElement value) &&
                        value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : "<id>";

            return Core.Localization.KeyConventions.Key(
                Core.Localization.KeyConventions.ClassNs, id, aspect);
        }

        static string Id(JsonElement root, string file, string pack, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("id", out JsonElement value) || value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(file, "id", "a class needs an id, as a string"));
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

            // refused here rather than three layers away in a locale audit
            if (!ContentId.NamesSomethingKeyable(scoped))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"'{scoped}' cannot be made into a key - " +
                    Core.Localization.KeyConventions.Explain(
                        Core.Localization.KeyConventions.ClassName(scoped))));
                return null;
            }

            return scoped;
        }

        // an item id, not a die; gear ids are a shared namespace, so axe is the engine's and warden_glaive this pack's
        static string Item(JsonElement root, string field, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value)) return null;

            if (value.ValueKind == JsonValueKind.Null) return null;

            if (value.ValueKind == JsonValueKind.String && ContentId.IsLocal(value.GetString()))
                return value.GetString();

            problems.Add(new ContentProblem(
                file, field,
                $"'{Shown(value)}' is not an item id - lowercase a-z, 0-9 and underscore, naming " +
                $"an item this pack's items/ folder ships or one the game does. Leave {field} out " +
                "for a class that starts without one"));

            return null;
        }

        // only the id shape is checked here; whether the ability exists is a folder question
        static IReadOnlyList<string> Kit(JsonElement root, string file, List<ContentProblem> problems)
        {
            var kit = new List<string>();

            if (!root.TryGetProperty("kit", out JsonElement value)) return kit;

            if (value.ValueKind == JsonValueKind.Null) return kit;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "kit",
                    "kit is a list of ability ids, like [ \"steady_guard\", \"riposte\" ]"));
                return kit;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"kit[{at++}]";

                if (entry.ValueKind != JsonValueKind.String || !ContentId.IsLocal(entry.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{Shown(entry)}' is not an ability id - lowercase a-z, 0-9 and " +
                        "underscore, naming a file in this pack's kits/ folder"));
                    continue;
                }

                string ability = entry.GetString();

                if (kit.Contains(ability, StringComparer.Ordinal))
                {
                    problems.Add(new ContentProblem(
                        file, where, $"'{ability}' is in this kit twice"));
                    continue;
                }

                kit.Add(ability);
            }

            return kit;
        }

        // bare is this pack's or the game's; a dotted id belongs to another pack and wants a dependencies line
        static string Mini(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("mini", out JsonElement value)) return "";

            if (value.ValueKind == JsonValueKind.Null) return "";

            if (value.ValueKind == JsonValueKind.String)
            {
                string id = value.GetString() ?? "";

                if (ContentId.IsLocal(id) ||
                    (ContentId.IsScoped(id) && ContentId.NamesSomethingKeyable(id)))
                    return id;
            }

            problems.Add(new ContentProblem(
                file, "mini",
                $"'{Shown(value)}' is not a mini id - either one this pack's minis/ folder " +
                "declares, one of the ones the game ships, or another pack's as '<pack>.<mini>'"));

            return "";
        }

        // 0 (left out) means whatever the hero normally has
        static int Nerve(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("nerve", out JsonElement value)) return 0;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int cap))
            {
                problems.Add(new ContentProblem(
                    file, "nerve",
                    $"'{Shown(value)}' is not a whole number - nerve is how many the hero can " +
                    "hold at once. Leave it out for the usual " +
                    $"{Core.Combat.Nerve.Cap}"));
                return 0;
            }

            if (cap < 1)
            {
                problems.Add(new ContentProblem(
                    file, "nerve",
                    $"{cap} is less than 1 - a hero who can hold no Nerve cannot reroll, push or " +
                    "shrug, which is the Nerve system switched off. Leave it out for the " +
                    $"usual {Core.Combat.Nerve.Cap}"));
                return 0;
            }

            return cap;
        }

        // exactly one of the three per step; picking one quietly would make the file and the game disagree
        static IReadOnlyList<Growth> Steps(JsonElement root, string file,
                                           List<ContentProblem> problems)
        {
            var steps = new List<Growth>();

            if (!root.TryGetProperty("growth", out JsonElement value)) return steps;

            if (value.ValueKind == JsonValueKind.Null) return steps;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "growth",
                    "growth is a list of the bigger rocks this class can be given, like " +
                    "[ { \"id\": \"veteran\", \"attribute\": \"might\" } ]"));
                return steps;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string where = $"growth[{at++}]";

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where, $"a growth step is an object and this is a {Named(entry.ValueKind)}"));
                    continue;
                }

                if (!entry.TryGetProperty("id", out JsonElement idValue) ||
                    idValue.ValueKind != JsonValueKind.String ||
                    !ContentId.IsLocal(idValue.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, where + ".id",
                        "a growth step needs an id - lowercase a-z, 0-9 and underscore. It is what " +
                        "a campaign hands out and what a save records, so it has to be stable"));
                    continue;
                }

                string id = idValue.GetString();

                if (steps.Any(s => s.Id == id))
                {
                    problems.Add(new ContentProblem(file, where + ".id", $"'{id}' is a step twice"));
                    continue;
                }

                Attr? attribute = Raised<Attr>(entry, "attribute", file, where, problems);
                Skill? skill = Raised<Skill>(entry, "skill", file, where, problems);
                string ability = Unlocked(entry, file, where, problems);

                int named = (attribute != null ? 1 : 0) + (skill != null ? 1 : 0) +
                            (ability != null ? 1 : 0);

                if (named != 1)
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        named == 0
                            ? "a growth step has to raise something - an 'attribute' a size, a " +
                              "'skill', or unlock an 'ability' into the kit"
                            : "a growth step raises exactly one thing - several under one name is " +
                              "several rewards a campaign cannot hand out separately"));
                    continue;
                }

                foreach (JsonProperty property in entry.EnumerateObject())
                    if (property.Name is not ("id" or "attribute" or "skill" or "ability"))
                        problems.Add(new ContentProblem(
                            file, where + "." + property.Name,
                            $"a growth step has no '{property.Name}' - it has id, and one of " +
                            "attribute, skill or ability"));

                steps.Add(new Growth(id, attribute, skill, ability));
            }

            return steps;
        }

        static TEnum? Raised<TEnum>(JsonElement entry, string field, string file, string where,
                                    List<ContentProblem> problems) where TEnum : struct, Enum
        {
            if (!entry.TryGetProperty(field, out JsonElement value)) return null;

            if (value.ValueKind == JsonValueKind.String &&
                Vocabulary.TryWord(value.GetString(), out TEnum word))
                return word;

            problems.Add(new ContentProblem(
                file, where + "." + field,
                $"'{Shown(value)}' is not a {field} - it is one of {Vocabulary.Offer<TEnum>()}"));

            return null;
        }

        static string Unlocked(JsonElement entry, string file, string where,
                               List<ContentProblem> problems)
        {
            if (!entry.TryGetProperty("ability", out JsonElement value)) return null;

            if (value.ValueKind == JsonValueKind.String && ContentId.IsLocal(value.GetString()))
                return value.GetString();

            problems.Add(new ContentProblem(
                file, where + ".ability",
                $"'{Shown(value)}' is not an ability id - lowercase a-z, 0-9 and underscore, " +
                "naming a file in this pack's kits/ folder"));

            return null;
        }

        static int Count(JsonElement root, string field, int least, string file,
                         List<ContentProblem> problems)
        {
            if (!root.TryGetProperty(field, out JsonElement value))
            {
                problems.Add(new ContentProblem(file, field, $"a class needs a {field}"));
                return least;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
            {
                problems.Add(new ContentProblem(file, field, $"'{Shown(value)}' is not a whole number"));
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
                        file, where, $"'{entry.Name}' is not one of {Vocabulary.Offer<TEnum>()}"));
                    continue;
                }

                if (entry.Value.ValueKind != JsonValueKind.String ||
                    !Vocabulary.TryDie(entry.Value.GetString(), out Die die))
                {
                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{Shown(entry.Value)}' is not a die - it is one of " +
                        $"{Vocabulary.Offer(Vocabulary.Dice)}"));
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

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();

        static string Shown(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Undefined => "nothing",
            _ => value.ToString(),
        };
    }
}

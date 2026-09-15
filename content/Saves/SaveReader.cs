using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Dice;

namespace Content.Saves
{
    public static class SaveReader
    {
        public static Read<SaveGame> Parse(string json, string file)
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
                // the one fatal case: a non-JSON file has no fields to default, so there's nothing to degrade to
                return Read<SaveGame>.Bad(new ContentProblem(
                    file, "", "this save is not JSON - " + bad.Message,
                    (int)(bad.LineNumber ?? 0) + 1));
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Read<SaveGame>.Bad(new ContentProblem(
                        file, "", $"a save is a JSON object and this is a {Named(root.ValueKind)}"));

                var save = new SaveGame
                {
                    Format = Number(root, "format", SaveFormat.Current, file, problems),
                    Engine = Engine(root, file, problems),
                    Campaign = Text(root, "campaign", file, problems),
                    CampaignFormat = Number(root, "campaign_format", 0, file, problems),
                    Chapter = Text(root, "chapter", file, problems),
                    Encounter = Text(root, "encounter", file, problems),
                    Round = Number(root, "round", 0, file, problems),
                    Turn = Number(root, "turn", -1, file, problems),
                    ActionsLeft = Number(root, "actions", 0, file, problems),
                };

                if (save.Format != SaveFormat.Current)
                    problems.Add(new ContentProblem(file, "format",
                                                    SaveFormat.Unfamiliar(save.Format)));

                if (root.TryGetProperty("sheet", out JsonElement sheet))
                    save.Sheet = Sheet(sheet, file, problems);

                if (root.TryGetProperty("hero", out JsonElement hero))
                    save.Hero = Actor(hero, file, "hero", problems);

                if (root.TryGetProperty("foes", out JsonElement foes))
                {
                    if (foes.ValueKind != JsonValueKind.Array)
                    {
                        problems.Add(new ContentProblem(
                            file, "foes", "foes is a list, and this save has none this build can read"));
                    }
                    else
                    {
                        int at = 0;

                        foreach (JsonElement one in foes.EnumerateArray())
                        {
                            SavedActor foe = Actor(one, file, $"foes[{at++}]", problems);

                            if (foe != null) save.Foes.Add(foe);
                        }
                    }
                }

                ReadFelt(root, save, file, problems);
                Unknown(root, Fields, file, "", problems);

                // always a value and the list: Read.Partial, the third state a save needs and a campaign doesn't
                return problems.Count == 0
                    ? Read<SaveGame>.Good(save)
                    : Read<SaveGame>.Partial(save, problems);
            }
        }

        static readonly string[] Fields =
        {
            "format", "engine", "campaign", "campaign_format", "chapter", "encounter",
            "round", "turn", "actions", "sheet", "hero", "foes", "felt",
        };

        static readonly string[] SheetFields =
        {
            "name", "class", "race", "background", "appearance", "vigor", "nerve", "strain",
            "notches", "erasures", "conditions", "wielded", "worn", "satchel", "growth",
        };

        static readonly string[] ActorFields =
        {
            "id", "vigor", "nerve", "seat", "initiative", "slot", "ordinal", "notches", "strain",
            "conditions", "wielded", "worn", "satchel", "growth", "at",
        };

        static readonly string[] DieFields = { "trait", "die", "value" };

        // Degrading, like everything else a save reads: a sheet missing a field gets the field's
        // own default rather than taking the save down with it. The ONE thing it cannot do without
        // is the class - a sheet with no class names no character, and there would be nothing to
        // assemble from it.
        static Content.Sheet.CharacterSheet Sheet(JsonElement element, string file,
                                                  List<ContentProblem> problems)
        {
            const string where = "sheet";

            if (element.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, where, $"this is a {Named(element.ValueKind)} and not a character sheet"));
                return null;
            }

            Unknown(element, SheetFields, file, where + ".", problems);

            var sheet = new Content.Sheet.CharacterSheet
            {
                Name = Text(element, "name", file, problems, where),
                ClassId = Text(element, "class", file, problems, where),
                RaceId = Text(element, "race", file, problems, where),
                BackgroundId = Text(element, "background", file, problems, where),
                Appearance = Text(element, "appearance", file, problems, where),
                Vigor = Number(element, "vigor", -1, file, problems, where),
                Nerve = Number(element, "nerve", Core.Combat.Nerve.StartOfDay, file, problems, where),
                Strain = Number(element, "strain", 0, file, problems, where),
                Notches = Number(element, "notches", 0, file, problems, where),
                Erasures = Number(element, "erasures", 0, file, problems, where),
            };

            if (sheet.ClassId.Length == 0)
            {
                problems.Add(new ContentProblem(
                    file, where + ".class",
                    "this sheet names no class, so there is no character to build from it - the " +
                    "save is read without one"));
                return null;
            }

            foreach (Condition condition in Words<Condition>(element, "conditions", file, where,
                                                             problems))
                if (!sheet.Conditions.Contains(condition)) sheet.Conditions.Add(condition);

            sheet.Wielded = Text(element, "wielded", file, problems, where);
            sheet.Worn = Text(element, "worn", file, problems, where);

            foreach (string id in Strings(element, "satchel", file, where, problems))
                sheet.Satchel.Add(id);

            // a step this build no longer offers is carried, not dropped; it returns if its pack does
            foreach (string step in Strings(element, "growth", file, where, problems))
                sheet.Growth.Add(step);

            return sheet;
        }

        static void ReadFelt(JsonElement root, SaveGame save, string file,
                             List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("felt", out JsonElement felt)) return;

            if (felt.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, "felt", "felt is the list of dice lying on the table - the fight " +
                                  "resumes with nothing thrown"));
                return;
            }

            int at = 0;

            foreach (JsonElement one in felt.EnumerateArray())
            {
                string where = $"felt[{at++}]";

                if (one.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, where, $"a die is an object and this is a {Named(one.ValueKind)}"));
                    continue;
                }

                Unknown(one, DieFields, file, where + ".", problems);

                string trait = Text(one, "trait", file, problems, where);
                Die die = ADie(one, file, where, problems);
                int value = Number(one, "value", 0, file, problems, where);

                // clamped, not dropped, so the throw keeps its shape; the likeliest hand-edit mistake
                if (die.IsReal() && (value < 1 || value > die.Sides()))
                {
                    int was = value;
                    value = Math.Clamp(value, 1, die.Sides());

                    problems.Add(new ContentProblem(
                        file, where + ".value",
                        $"a {Vocabulary.NameOf(die)} cannot show {was} - read as {value}"));
                }

                if (!die.IsReal())
                {
                    problems.Add(new ContentProblem(
                        file, where + ".die", "this die is left off the felt"));
                    continue;
                }

                save.Felt.Add(new SavedDie(trait, die, value));
            }
        }

        static SavedActor Actor(JsonElement element, string file, string where,
                                List<ContentProblem> problems)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new ContentProblem(
                    file, where, $"this is a {Named(element.ValueKind)} and not somebody"));
                return null;
            }

            Unknown(element, ActorFields, file, where + ".", problems);

            string id = Text(element, "id", file, problems, where);

            // the one field with nothing to degrade to: an actor with no id names nobody
            if (id.Length == 0)
            {
                problems.Add(new ContentProblem(
                    file, where + ".id", "nobody is named here, so this one is left out"));
                return null;
            }

            var actor = new SavedActor
            {
                Id = id,
                Vigor = Number(element, "vigor", -1, file, problems, where),
                Nerve = Number(element, "nerve", 0, file, problems, where),
                Seat = Number(element, "seat", -1, file, problems, where),
                Initiative = Number(element, "initiative", 0, file, problems, where),
                Slot = Number(element, "slot", 0, file, problems, where),
                Ordinal = Number(element, "ordinal", 0, file, problems, where),
                Notches = Number(element, "notches", 0, file, problems, where),
                Strain = Number(element, "strain", 0, file, problems, where),
                Wielded = Text(element, "wielded", file, problems, where),
                Worn = Text(element, "worn", file, problems, where),
            };

            foreach (Condition condition in Words<Condition>(element, "conditions", file, where,
                                                             problems))
                if (!actor.Conditions.Contains(condition)) actor.Conditions.Add(condition);

            foreach (string item in Strings(element, "satchel", file, where, problems))
                actor.Satchel.Add(item);

            // a step this build no longer offers is carried, not dropped; it returns if its pack comes back
            foreach (string step in Strings(element, "growth", file, where, problems))
                actor.Growth.Add(step);

            ReadSquare(element, actor, file, where, problems);

            return actor;
        }

        static void ReadSquare(JsonElement element, SavedActor actor, string file, string where,
                               List<ContentProblem> problems)
        {
            if (!element.TryGetProperty("at", out JsonElement at)) return;

            if (at.ValueKind == JsonValueKind.Null) return;

            if (at.ValueKind != JsonValueKind.Array || at.GetArrayLength() != 2)
            {
                problems.Add(new ContentProblem(
                    file, where + ".at",
                    "a square is two whole numbers, like [3, 2] - this one is left off the board"));
                return;
            }

            int index = 0;
            var square = new int[2];

            foreach (JsonElement number in at.EnumerateArray())
            {
                if (number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out square[index]))
                {
                    problems.Add(new ContentProblem(
                        file, where + $".at[{index}]",
                        $"'{Shown(number)}' is not a whole number - this one is left off the board"));
                    return;
                }

                index++;
            }

            actor.X = square[0];
            actor.Y = square[1];
        }


        static void Unknown(JsonElement element, string[] known, string file, string prefix,
                            List<ContentProblem> problems)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (Array.IndexOf(known, property.Name) >= 0) continue;

                // not a failure: an unknown field is a later build's, or a typo, and either way it's said and skipped
                problems.Add(new ContentProblem(
                    file, prefix + property.Name,
                    $"this build does not know what '{property.Name}' is, and ignored it"));
            }
        }

        static string Text(JsonElement element, string field, string file,
                           List<ContentProblem> problems, string where = "")
        {
            if (!element.TryGetProperty(field, out JsonElement value)) return "";

            if (value.ValueKind == JsonValueKind.Null) return "";

            if (value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new ContentProblem(
                    file, Where(where, field), $"'{Shown(value)}' is not a word - read as nothing"));
                return "";
            }

            return value.GetString() ?? "";
        }

        static int Number(JsonElement element, string field, int fallback, string file,
                          List<ContentProblem> problems, string where = "")
        {
            if (!element.TryGetProperty(field, out JsonElement value)) return fallback;

            if (value.ValueKind == JsonValueKind.Null) return fallback;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
            {
                problems.Add(new ContentProblem(
                    file, Where(where, field),
                    $"'{Shown(value)}' is not a whole number - read as {fallback}"));
                return fallback;
            }

            return number;
        }

        static Version Engine(JsonElement root, string file, List<ContentProblem> problems)
        {
            string text = Text(root, "engine", file, problems);

            if (text.Length == 0) return Core.EngineVersion.Current;

            if (Version.TryParse(text, out Version engine)) return engine;

            problems.Add(new ContentProblem(
                file, "engine", $"'{text}' is not a version - read as {Core.EngineVersion.Current}"));

            return Core.EngineVersion.Current;
        }

        static Die ADie(JsonElement element, string file, string where, List<ContentProblem> problems)
        {
            string text = Text(element, "die", file, problems, where);

            if (Vocabulary.TryDie(text, out Die die)) return die;

            problems.Add(new ContentProblem(
                file, Where(where, "die"),
                $"'{text}' is not a die - it is one of {Vocabulary.Offer(Vocabulary.Dice)}"));

            return Die.None;
        }

        static IEnumerable<TEnum> Words<TEnum>(JsonElement element, string field, string file,
                                               string where, List<ContentProblem> problems)
            where TEnum : struct, Enum
        {
            if (!element.TryGetProperty(field, out JsonElement value)) yield break;

            if (value.ValueKind != JsonValueKind.Array)
            {
                if (value.ValueKind != JsonValueKind.Null)
                    problems.Add(new ContentProblem(
                        file, Where(where, field), "this is a list of words - read as none"));

                yield break;
            }

            int at = 0;

            foreach (JsonElement one in value.EnumerateArray())
            {
                string spot = Where(where, field) + $"[{at++}]";

                if (one.ValueKind != JsonValueKind.String ||
                    !Vocabulary.TryWord(one.GetString(), out TEnum word))
                {
                    problems.Add(new ContentProblem(
                        file, spot,
                        $"'{Shown(one)}' is not one of {Vocabulary.Offer<TEnum>()} - left off"));
                    continue;
                }

                yield return word;
            }
        }

        static IEnumerable<string> Strings(JsonElement element, string field, string file,
                                           string where, List<ContentProblem> problems)
        {
            if (!element.TryGetProperty(field, out JsonElement value)) yield break;

            if (value.ValueKind != JsonValueKind.Array)
            {
                if (value.ValueKind != JsonValueKind.Null)
                    problems.Add(new ContentProblem(
                        file, Where(where, field), "this is a list of ids - read as none"));

                yield break;
            }

            int at = 0;

            foreach (JsonElement one in value.EnumerateArray())
            {
                string spot = Where(where, field) + $"[{at++}]";

                if (one.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(one.GetString()))
                {
                    problems.Add(new ContentProblem(
                        file, spot, $"'{Shown(one)}' is not an id - left off"));
                    continue;
                }

                yield return one.GetString();
            }
        }

        static string Where(string where, string field) =>
            where.Length == 0 ? field : where + "." + field;

        static string Shown(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Undefined => "nothing",
            _ => value.ToString(),
        };

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();


        // never throws; a save that can't be opened is a sentence, for the reason this whole file exists
        public static Read<SaveGame> From(string path)
        {
            string file = Path.GetFileName(path) ?? "";

            try
            {
                if (!File.Exists(path))
                    return Read<SaveGame>.Bad(new ContentProblem(file, "", "there is no such save"));

                return Parse(File.ReadAllText(path), file);
            }
            catch (Exception could)
            {
                return Read<SaveGame>.Bad(new ContentProblem(
                    file, "", "could not be read - " + could.Message));
            }
        }
    }
}

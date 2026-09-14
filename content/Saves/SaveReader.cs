using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Dice;

namespace Content.Saves
{
    // A SAVE, READ BACK (CONTENT_PIPELINE.md P6).
    //
    // IT DEGRADES; IT DOES NOT REFUSE. This is the one reader in the project that does not follow
    // `MapReader`'s "refuse and name" discipline, and the difference is whose file it is. A
    // campaign is somebody else's content and a broken one should not be played at all - half a
    // stranger's campaign is a game that plays wrong. A save is the player's own afternoon, and
    // the worst possible outcome is declining to open it. So every field that cannot be read is a
    // sentence and a default, and the only thing that stops a load is a file that is not JSON.
    //
    // `SEAMS.md` section 9 is the whole of why this is possible at all: "state shaped for save/load
    // degrades; it doesn't throw". Nothing in here computes a second field from a first, so there
    // is no pair that can disagree and no case where the honest answer is an exception.
    //
    // A HAND-EDITED SAVE IS THE NORMAL CASE, not an attack. "In 2036 you will want to hand-edit a
    // save" means somebody WILL open this file and change a number, and every message below is
    // written to that person - the field, what was wrong with it, and what was used instead.
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
                // THE ONE FATAL CASE. There is nothing to degrade to: a file that is not JSON has
                // no fields to take defaults for, and a blank save presented as the player's would
                // be worse than saying so
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

                // SAID, NOT REFUSED. A save from a later build may have fields this one has never
                // heard of, and every one of them is reported by `Unknown` below - so "it is read
                // as far as it can be" is a claim the file itself backs up
                if (save.Format != SaveFormat.Current)
                    problems.Add(new ContentProblem(file, "format",
                                                    SaveFormat.Unfamiliar(save.Format)));

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

                // ALWAYS A VALUE *AND* THE LIST. Every problem above is a field that took its
                // default, so what comes back is a save the caller can load and a list the caller
                // can show - `Read<T>.Partial`, the third state a save needs and a campaign does
                // not. `Ok` is false and the save is right there
                return problems.Count == 0
                    ? Read<SaveGame>.Good(save)
                    : Read<SaveGame>.Partial(save, problems);
            }
        }

        static readonly string[] Fields =
        {
            "format", "engine", "campaign", "campaign_format", "chapter", "encounter",
            "round", "turn", "actions", "hero", "foes", "felt",
        };

        static readonly string[] ActorFields =
        {
            "id", "vigor", "nerve", "seat", "initiative", "slot", "ordinal", "notches", "strain",
            "conditions", "wielded", "worn", "satchel", "at",
        };

        static readonly string[] DieFields = { "trait", "die", "value" };

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

                // A DIE SHOWING A FACE IT DOES NOT HAVE is the most likely hand-edit mistake there
                // is, and the marks drawn from it would be a claim about the rules that the rules
                // never made. Clamped rather than dropped, so the throw keeps its shape
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

            // THE ONE FIELD WITH NOTHING TO DEGRADE TO. Everything else about an actor can take a
            // default; who they are cannot, and an actor with no id is a line in the file that
            // names nobody
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

        // ---- fields ----

        static void Unknown(JsonElement element, string[] known, string file, string prefix,
                            List<ContentProblem> problems)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (Array.IndexOf(known, property.Name) >= 0) continue;

                // NOT A FAILURE. A save written by a later build carries fields this one has never
                // heard of, and the right thing to do with them is say so and carry on - which is
                // also exactly what a typo in a hand-edited save looks like from in here
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

        // ---- and off a disk ----

        // Never throws. A save that cannot be opened is a sentence, for exactly the reason the
        // rest of this file exists
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

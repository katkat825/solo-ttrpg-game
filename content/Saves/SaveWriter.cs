using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Content.Schema;
using Core.Characters;

namespace Content.Saves
{
    // A SAVE, WRITTEN OUT (CONTENT_PIPELINE.md P6).
    //
    // HAND-EDITABLE IS A PROPERTY OF THE WRITER, not of the format. Indented, one field per line,
    // fields in the order a person would want to read them, words rather than numbers for
    // everything that has a word (`"winded"`, `"d8"`) - because "in 2036 you will want to
    // hand-edit a save, and you'll be glad it's readable" is a claim about what comes out of
    // here. `JsonSerializer` with default options would produce a correct file that nobody wants
    // to open.
    //
    // AND THE SAME VOCABULARY THE CAMPAIGN FILES USE (`Content.Schema.Vocabulary`), so a save and
    // a statblock spell a die and a condition the same way. Two dialects would be one more thing
    // to learn for no reason at all.
    public static class SaveWriter
    {
        public static string Write(SaveGame save)
        {
            var buffer = new MemoryStream();

            using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();

                // WHAT BUILT IT, FIRST, because it is the first thing anybody looking at a strange
                // save needs and the first thing a reader checks
                json.WriteNumber("format", save.Format);
                json.WriteString("engine", (save.Engine ?? Core.EngineVersion.Current).ToString());

                json.WriteString("campaign", save.Campaign ?? "");
                json.WriteNumber("campaign_format", save.CampaignFormat);
                json.WriteString("chapter", save.Chapter ?? "");
                json.WriteString("encounter", save.Encounter ?? "");

                json.WriteNumber("round", save.Round);
                json.WriteNumber("turn", save.Turn);
                json.WriteNumber("actions", save.ActionsLeft);

                if (save.Hero != null)
                {
                    json.WritePropertyName("hero");
                    WriteActor(json, save.Hero);
                }

                json.WritePropertyName("foes");
                json.WriteStartArray();

                foreach (SavedActor foe in save.Foes) WriteActor(json, foe);

                json.WriteEndArray();

                json.WritePropertyName("felt");
                json.WriteStartArray();

                foreach (SavedDie die in save.Felt)
                {
                    json.WriteStartObject();
                    json.WriteString("trait", die.Trait ?? "");
                    json.WriteString("die", Vocabulary.NameOf(die.Die));
                    json.WriteNumber("value", die.Value);
                    json.WriteEndObject();
                }

                json.WriteEndArray();

                json.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        static void WriteActor(Utf8JsonWriter json, SavedActor actor)
        {
            json.WriteStartObject();

            json.WriteString("id", actor.Id ?? "");
            json.WriteNumber("vigor", actor.Vigor);
            json.WriteNumber("nerve", actor.Nerve);

            // ONLY WHAT IS TRUE OF THIS ONE. A save with `"ordinal": 0, "notches": 0, "strain": 0`
            // on every line of a room of eight is a file nobody can scan - the interesting fields
            // are the ones that are there
            if (actor.Seat >= 0) json.WriteNumber("seat", actor.Seat);
            if (actor.Initiative > 0) json.WriteNumber("initiative", actor.Initiative);
            if (actor.Slot > 0) json.WriteNumber("slot", actor.Slot);
            if (actor.Ordinal > 0) json.WriteNumber("ordinal", actor.Ordinal);
            if (actor.Notches > 0) json.WriteNumber("notches", actor.Notches);
            if (actor.Strain > 0) json.WriteNumber("strain", actor.Strain);

            if (actor.Conditions.Count > 0)
            {
                json.WritePropertyName("conditions");
                json.WriteStartArray();

                foreach (Condition condition in actor.Conditions)
                    json.WriteStringValue(Vocabulary.NameOf(condition));

                json.WriteEndArray();
            }

            if (!string.IsNullOrEmpty(actor.Wielded)) json.WriteString("wielded", actor.Wielded);
            if (!string.IsNullOrEmpty(actor.Worn)) json.WriteString("worn", actor.Worn);

            if (actor.Satchel.Count > 0)
            {
                json.WritePropertyName("satchel");
                json.WriteStartArray();

                foreach (string item in actor.Satchel) json.WriteStringValue(item ?? "");

                json.WriteEndArray();
            }

            // A PAIR RATHER THAN TWO FIELDS, because a square is one fact and half of one is
            // meaningless - and because `[3, 2]` is how anybody would write a square down
            if (actor.OnTheBoard)
            {
                json.WritePropertyName("at");
                json.WriteStartArray();
                json.WriteNumberValue(actor.X.Value);
                json.WriteNumberValue(actor.Y.Value);
                json.WriteEndArray();
            }

            json.WriteEndObject();
        }

        // ---- and onto a disk ----

        // Never throws. A save that cannot be written is a sentence the caller shows the player,
        // not an exception out of the middle of a fight - which is the same rule the campaign
        // readers follow and for a much more pressing reason: the player is standing there
        public static ContentProblem To(string path, SaveGame save)
        {
            try
            {
                string folder = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                File.WriteAllText(path, Write(save));

                return null;
            }
            catch (System.Exception could)
            {
                return new ContentProblem(Path.GetFileName(path) ?? "", "",
                                          "could not be written - " + could.Message);
            }
        }
    }
}

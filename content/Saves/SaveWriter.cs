using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Content.Schema;
using Core.Characters;

namespace Content.Saves
{
    public static class SaveWriter
    {
        public static string Write(SaveGame save)
        {
            var buffer = new MemoryStream();

            using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();

                json.WriteNumber("format", save.Format);
                json.WriteString("engine", (save.Engine ?? Core.EngineVersion.Current).ToString());

                json.WriteString("campaign", save.Campaign ?? "");
                json.WriteNumber("campaign_format", save.CampaignFormat);
                json.WriteString("chapter", save.Chapter ?? "");
                json.WriteString("encounter", save.Encounter ?? "");

                json.WriteNumber("round", save.Round);
                json.WriteNumber("turn", save.Turn);
                json.WriteNumber("actions", save.ActionsLeft);

                if (save.HasASheet)
                {
                    json.WritePropertyName("sheet");
                    WriteSheet(json, save.Sheet);
                }

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

        // The sheet, written the way it is filled in: the blanks first, then what play wrote on
        // it. Empty fields are left out, so the file reads like a sheet and not like a form.
        static void WriteSheet(Utf8JsonWriter json, Content.Sheet.CharacterSheet sheet)
        {
            json.WriteStartObject();

            if (!string.IsNullOrEmpty(sheet.Name)) json.WriteString("name", sheet.Name);

            json.WriteString("class", sheet.ClassId ?? "");

            if (!string.IsNullOrEmpty(sheet.RaceId)) json.WriteString("race", sheet.RaceId);
            if (!string.IsNullOrEmpty(sheet.BackgroundId)) json.WriteString("background", sheet.BackgroundId);
            if (!string.IsNullOrEmpty(sheet.Appearance)) json.WriteString("appearance", sheet.Appearance);

            json.WriteNumber("vigor", sheet.Vigor);
            json.WriteNumber("nerve", sheet.Nerve);

            if (sheet.Strain > 0) json.WriteNumber("strain", sheet.Strain);
            if (sheet.Notches > 0) json.WriteNumber("notches", sheet.Notches);
            if (sheet.Erasures > 0) json.WriteNumber("erasures", sheet.Erasures);

            if (sheet.Conditions.Count > 0)
            {
                json.WritePropertyName("conditions");
                json.WriteStartArray();

                foreach (Condition condition in sheet.Conditions)
                    json.WriteStringValue(Vocabulary.NameOf(condition));

                json.WriteEndArray();
            }

            if (!string.IsNullOrEmpty(sheet.Wielded)) json.WriteString("wielded", sheet.Wielded);
            if (!string.IsNullOrEmpty(sheet.Worn)) json.WriteString("worn", sheet.Worn);

            Strings(json, "satchel", sheet.Satchel);
            Strings(json, "growth", sheet.Growth);

            json.WriteEndObject();
        }

        static void Strings(Utf8JsonWriter json, string name, IList<string> list)
        {
            if (list.Count == 0) return;

            json.WritePropertyName(name);
            json.WriteStartArray();

            foreach (string one in list) json.WriteStringValue(one ?? "");

            json.WriteEndArray();
        }

        static void WriteActor(Utf8JsonWriter json, SavedActor actor)
        {
            json.WriteStartObject();

            json.WriteString("id", actor.Id ?? "");
            json.WriteNumber("vigor", actor.Vigor);
            json.WriteNumber("nerve", actor.Nerve);

            // omit zeros so the interesting fields are the ones actually written
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

            if (actor.Growth.Count > 0)
            {
                json.WritePropertyName("growth");
                json.WriteStartArray();

                foreach (string step in actor.Growth) json.WriteStringValue(step ?? "");

                json.WriteEndArray();
            }

            // a pair, not two fields: [3, 2] is how anyone writes a square, and half of one is meaningless
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


        // never throws; a save that can't be written is a sentence for the player, not an exception mid-fight
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

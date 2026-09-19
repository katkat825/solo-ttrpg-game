using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Room
{
    // WHAT IS ON THE TABLE, AS A LIST OF DISCRETE OBJECTS.
    //
    // Prop is the list of things in the ROOM that replace a menu. This is the list of things on the
    // TABLE, and it exists for a different reason: none of them is welded to the table, so each can
    // be swapped for another of the same kind - a campaign's own screen, a class's own tray felt, a
    // leather folio for the sheet you earned finishing something.
    //
    // The mat is the first member and the one that proves the point: it is swapped every time you
    // walk into another place, and nothing else on the table moves when it does.
    public enum TableProp
    {
        // the place you are in. Swapped by walking, not by earning
        Mat,

        Tray,

        Screen,

        Sheet,

        // the physical turn-order object you touch to pass the turn
        Initiative,

        // the dice themselves, which are a skin and never a number
        Dice,
    }

    public static class TableProps
    {
        public static string Word(this TableProp prop) => prop.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words =>
            Enum.GetValues<TableProp>().Select(Word).ToArray();

        // ui.*, because the table is the engine's and every campaign is played on the same one
        public static string NameKey(TableProp prop) =>
            KeyConventions.Key(KeyConventions.UiNs, prop.Word(), "name");

        public static IEnumerable<string> Keys()
        {
            foreach (TableProp prop in Enum.GetValues<TableProp>()) yield return NameKey(prop);
        }

        // THE ONE THAT IS NOT COSMETIC, and it is not an exception to the rule - it is the rule
        // read the other way. Every other prop here is swapped for a nicer one and plays the same;
        // the mat is swapped because you walked somewhere, which is not a cosmetic at all.
        public static bool IsEarned(this TableProp prop) => prop != TableProp.Mat;

        public static bool TryWord(string word, out TableProp prop)
        {
            prop = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (TableProp one in Enum.GetValues<TableProp>())
            {
                if (Word(one) != trimmed) continue;

                prop = one;
                return true;
            }

            return false;
        }
    }
}

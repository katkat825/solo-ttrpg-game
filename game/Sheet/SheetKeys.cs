using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Sheet
{
    // The words printed on the paper: the labels beside each blank, and the names of the objects in
    // the room. These are the ENGINE's - every sheet has a "Class" line whatever campaign you play
    // - so they live under ui.* in game/locale/ (CONVENTIONS.md section 7, ROOM_AND_SHEET.md
    // architecture notes). What goes IN a blank is a pack's, and is keyed under class.*.
    //
    // Derived from the enums rather than listed, for the same reason EngineKeys is: a blank added
    // to the sheet or a prop added to the room should fail the locale audit until it has words.
    public static class SheetKeys
    {
        public const string Subject = "character_sheet";

        public static string Title => KeyConventions.Key(KeyConventions.UiNs, Subject, "title");

        // the line's own label: ui.character_sheet.name, ui.character_sheet.class, ...
        public static string Label(Line line) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, line.Word());

        // what an unfilled blank reads as, so a sheet mid-filling never shows an empty line
        public static string Blank => KeyConventions.Key(KeyConventions.UiNs, Subject, "blank");

        // "Nerve 3 of 5", above the pips (R2)
        public static string Nerve => KeyConventions.Key(KeyConventions.UiNs, Subject, "nerve");

        public static string Vigor => KeyConventions.Key(KeyConventions.UiNs, Subject, "vigor");

        public static IEnumerable<string> All()
        {
            yield return Title;
            yield return Blank;
            yield return Nerve;
            yield return Vigor;

            foreach (Line line in Enum.GetValues<Line>()) yield return Label(line);

            foreach (Game.Room.Prop prop in Enum.GetValues<Game.Room.Prop>())
                yield return Game.Room.Props.NameKey(prop);
        }

        // both of these count numbers in, so a translation that dropped the placeholder would read
        // as a deliberate choice rather than as a bug
        public static bool TakesAnArgument(string key) => key == Nerve || key == Vigor;
    }

    // The lines on the sheet, in the order they are printed. Closed, because a sheet has the lines
    // it has - a new one is a new line drawn on the paper and a new label to translate, which is
    // exactly the kind of change that should be visible.
    public enum Line
    {
        Name,

        Race,

        Class,

        Background,

        Appearance,
    }

    public static class Lines
    {
        public static string Word(this Line line) => line.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words => Enum.GetValues<Line>().Select(Word).ToArray();

        // which lines are dropdowns - the ones where a real sheet has a blank and the game has a
        // list of cards to fill it from. Name and appearance are written in by hand.
        public static bool IsADropdown(this Line line) =>
            line is Line.Race or Line.Class or Line.Background;
    }
}

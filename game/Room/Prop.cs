using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Room
{
    // THE ROOM IS THE ENTIRE UI (R3, THE_TABLE.md section 6). There are no menu screens anywhere in
    // this game, and this enum is the reason that is checkable rather than aspirational: every
    // place a normal RPG opens a panel, this game has an OBJECT, and here is the list of them.
    //
    // Each line of THE_TABLE.md's table is one member here, and the member says what the object
    // replaces. A milestone that wants a panel is a milestone that is wrong; find the object that
    // panel should have been, and add it here.
    public enum Prop
    {
        // save files / campaign select. A save is a box you pick up, not a slot you select
        Shelf,

        // the character screen. It is on the table rather than in a window (R0)
        Sheet,

        // the quest log
        Corkboard,

        // the collection screen, and where dice you have earned end up
        DiceCase,

        // the bestiary: the box lid with the loose minis in it
        BoxLid,

        // time of day, weather, season
        Window,

        // quit
        Door,
    }

    public static class Props
    {
        public static string Word(this Prop prop) => prop switch
        {
            Prop.DiceCase => "dice_case",
            Prop.BoxLid => "box_lid",
            _ => prop.ToString().ToLowerInvariant(),
        };

        public static IReadOnlyList<string> Words => Enum.GetValues<Prop>().Select(Word).ToArray();

        // ui.*, because the room is the engine's and every campaign is played in the same one
        public static string NameKey(Prop prop) =>
            KeyConventions.Key(KeyConventions.UiNs, prop.Word(), "name");

        public static bool TryWord(string word, out Prop prop)
        {
            prop = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Prop one in Enum.GetValues<Prop>())
            {
                if (Word(one) != trimmed) continue;

                prop = one;
                return true;
            }

            return false;
        }

        // what a normal RPG would have opened instead. Developer-facing, never localized - it is
        // here so the deletion is written down beside the thing that did the deleting.
        public static string Replaces(this Prop prop) => prop switch
        {
            Prop.Shelf => "the save menu and the campaign select",
            Prop.Sheet => "the character screen, the level-up modal and the inventory panel",
            Prop.Corkboard => "the quest log",
            Prop.DiceCase => "the collection and unlocks screen",
            Prop.BoxLid => "the bestiary",
            Prop.Window => "a time-of-day readout",
            Prop.Door => "the quit menu",
            _ => "nothing",
        };

        // the one object that asks before it does what it does; everything else is reversible
        public static bool Confirms(this Prop prop) => prop == Prop.Door;
    }
}

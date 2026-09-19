using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Godot;

namespace Game.Access
{
    // EVERYTHING THE KEYBOARD CAN DO, AND THEREFORE EVERYTHING THERE IS TO REBIND (AX1).
    //
    // Closed, and short on purpose. The room is made of objects rather than controls, so a keyboard
    // player needs a way to move a hand between them, a way to press the one it is on, and the two
    // things a lost player reaches for - the rules, and "where are we". There is no movement key,
    // no camera key and no hotbar, because there is nothing for them to do.
    //
    // These ARE the Input actions: Word() is the name in the InputMap, so throw_dice keeps the name
    // it has had since M1 and nothing has to be kept in step with project.godot by hand.
    public enum Act
    {
        // the mouse's own button keeps its action (place_piece); this is the same press, by keyboard
        Touch,

        ReachNext,

        ReachBack,

        ThrowDice,

        // the "?" on the table, from anywhere - the one control a lost player should not have to find
        Help,

        // "what was I doing / where are we", askable at any point
        WhereAreWe,

        // the screen reader, on and off, without going through the book to get there
        ReadAloud,
    }

    public static class Acts
    {
        public static string Word(this Act act) => act switch
        {
            Act.Touch => "touch",
            Act.ReachNext => "reach_next",
            Act.ReachBack => "reach_back",
            Act.ThrowDice => "throw_dice",
            Act.WhereAreWe => "where_are_we",
            Act.ReadAloud => "read_aloud",
            _ => act.ToString().ToLowerInvariant(),
        };

        public static IReadOnlyList<string> Words => Enum.GetValues<Act>().Select(Word).ToArray();

        public const string Subject = "act";

        // ui.act.reach_next.name - "Reach for the next thing - {0}", the key counted in, so the page
        // that lists the bindings reads as sentences rather than as a table of two columns
        public static string NameKey(this Act act) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(act), "name");

        // what stands in the {0} while the page is waiting for you to press something
        public static string Waiting => KeyConventions.Key(KeyConventions.UiNs, Subject, "waiting");

        public static IEnumerable<string> Keys()
        {
            foreach (Act act in Enum.GetValues<Act>()) yield return NameKey(act);

            yield return Waiting;
        }

        // every act's line counts its key in; nothing else here does
        public static bool TakesAnArgument(string key) =>
            key != Waiting && Enum.GetValues<Act>().Any(a => NameKey(a) == key);

        // THE DEFAULTS, and each one is the key a player already expects. Tab walks a list in every
        // piece of software there is, Enter presses the thing, and Space has thrown the dice since
        // the tray existed.
        public static Key Standard(this Act act) => act switch
        {
            Act.Touch => Key.Enter,
            Act.ReachNext => Key.Tab,
            Act.ReachBack => Key.Tab,
            Act.ThrowDice => Key.Space,
            Act.Help => Key.F1,
            Act.WhereAreWe => Key.F2,
            Act.ReadAloud => Key.F3,
            _ => Key.None,
        };

        // reaching backward is the one that shares a key, the way it does everywhere
        public static bool Shifted(this Act act) => act == Act.ReachBack;

        // the acts a hand needs to walk the room. Named, because a rebinding that left one of these
        // unbound would leave a keyboard player with no way back out of wherever they are
        public static bool Essential(this Act act) =>
            act is Act.Touch or Act.ReachNext or Act.ReachBack;

        public static bool TryWord(string word, out Act act)
        {
            act = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Act one in Enum.GetValues<Act>())
            {
                if (Word(one) != trimmed) continue;

                act = one;
                return true;
            }

            return false;
        }
    }
}

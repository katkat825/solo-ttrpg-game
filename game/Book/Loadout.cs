using System;
using System.Collections.Generic;
using System.Linq;
using Game.Room;

namespace Game.Book
{
    // WHAT YOU OWN, AND WHICH OF IT IS ON THE TABLE TONIGHT (BK6).
    //
    // The bag of dice is the flavour; the choice is a page in the book, because a choice between
    // things you have earned is a list and a list wants a page. Dressing already holds which skin
    // each prop is wearing - this is the half Dressing deliberately has no opinion about: whether
    // you have the thing at all.
    //
    // COSMETICS NEVER AFFECT MECHANICS, and there is nowhere here for them to. There is no die on
    // this type, no bonus and no modifier of any kind, exactly as on Dressing: the moment a nicer
    // tray is a BETTER tray, every session becomes a question about your inventory instead of the
    // dungeon.
    public sealed class Loadout
    {
        readonly Dictionary<TableProp, List<string>> _owned =
            new Dictionary<TableProp, List<string>>();

        public Loadout(Dressing dressing = null)
        {
            Dressing = dressing ?? new Dressing();
        }

        public Dressing Dressing { get; }

        // the plain one is always owned - a prop wearing nothing is a prop that is not there, so
        // there has to be something to fall back to
        public IReadOnlyList<string> Owned(TableProp prop)
        {
            var list = new List<string> { Dressing.Plain };

            if (_owned.TryGetValue(prop, out List<string> mine))
                foreach (string skin in mine)
                    if (!list.Contains(skin, StringComparer.Ordinal)) list.Add(skin);

            return list;
        }

        public bool Has(TableProp prop, string skin) =>
            skin != null && Owned(prop).Contains(skin.Trim(), StringComparer.Ordinal);

        // earned, found, shipped with the game - this does not care which. False for a name it
        // already had, so a caller can tell a new thing from a re-read of the same folder
        public bool Earn(TableProp prop, string skin)
        {
            if (string.IsNullOrWhiteSpace(skin)) return false;

            string trimmed = skin.Trim();

            if (Has(prop, trimmed)) return false;

            if (!_owned.TryGetValue(prop, out List<string> mine))
                _owned[prop] = mine = new List<string>();

            mine.Add(trimmed);

            return true;
        }

        public int Count(TableProp prop) => Owned(prop).Count;

        // A SKIN YOU DO NOT OWN IS NOT A SKIN YOU CAN WEAR, and that refusal lives here rather than
        // in Dressing: Dressing is what is on the table, and a table can be dressed by a check, by a
        // save or by a campaign without any of them having to know what the player has earned.
        public bool Wear(TableProp prop, string skin) =>
            Has(prop, skin) && Dressing.Wear(prop, skin);

        public string Wearing(TableProp prop) => Dressing.Wearing(prop);

        // back to plain, which is the one swap that can never be refused
        public bool Strip(TableProp prop) => Dressing.Strip(prop);

        // every prop with more than one thing to wear: what the loadout page has a line for. A prop
        // you own one of is not a choice
        public IEnumerable<TableProp> Choices =>
            Enum.GetValues<TableProp>().Where(p => p.IsEarned() && Count(p) > 1);

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"loadout: {string.Join(", ", Enum.GetValues<TableProp>().Select(p => $"{p.Word()} {Count(p)}"))}";
    }
}

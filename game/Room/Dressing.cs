using System;
using System.Collections.Generic;

namespace Game.Room
{
    // WHICH VERSION OF EACH PROP IS ON THE TABLE TONIGHT.
    //
    // A prop and its skin, and nothing else. There is deliberately no die on this type, no bonus,
    // no defence and no modifier of any kind, and there must never be: the moment a nicer tray is
    // a BETTER tray, every session becomes a question about your inventory instead of the dungeon.
    // A finer tray is finer. That is the whole of what it is.
    //
    // It is a name per prop rather than a resource, so what a skin actually IS stays the business
    // of whatever wears it - the tray already loads its own felt out of a folder, and a screen will
    // load art the same way. Nothing here has to know.
    public sealed class Dressing
    {
        // what every table starts with, and what a prop falls back to. Named rather than empty,
        // because a prop wearing nothing is a prop that is not there
        public const string Plain = "plain";

        readonly Dictionary<TableProp, string> _worn = new Dictionary<TableProp, string>();

        public string Wearing(TableProp prop) =>
            _worn.TryGetValue(prop, out string skin) && skin.Length > 0 ? skin : Plain;

        public bool IsPlain(TableProp prop) => Wearing(prop) == Plain;

        // false for a skin with no name, which would be a prop quietly disappearing
        public bool Wear(TableProp prop, string skin)
        {
            if (string.IsNullOrWhiteSpace(skin)) return false;

            string was = Wearing(prop);

            _worn[prop] = skin.Trim();

            return was != Wearing(prop);
        }

        public bool Strip(TableProp prop) => _worn.Remove(prop);

        public void Plainly()
        {
            _worn.Clear();
        }

        // every prop and what it is wearing, in the enum's order, so a table can be dressed by
        // walking it and nothing can be left out
        public IEnumerable<KeyValuePair<TableProp, string>> Worn
        {
            get
            {
                foreach (TableProp prop in Enum.GetValues<TableProp>())
                    yield return new KeyValuePair<TableProp, string>(prop, Wearing(prop));
            }
        }

        public int Swapped
        {
            get
            {
                int count = 0;

                foreach (KeyValuePair<TableProp, string> one in Worn)
                    if (one.Value != Plain) count++;

                return count;
            }
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"dressed: {Swapped} of {Enum.GetValues<TableProp>().Length} swapped - " +
            string.Join(", ", System.Linq.Enumerable.Select(Worn, w => $"{w.Key.Word()}={w.Value}"));
    }
}

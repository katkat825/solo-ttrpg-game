using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Classes;
using Content.Sheet;

namespace Game.Sheet
{
    // FILLING IN THE SHEET (R0). No creation wizard, no stat allocator with a Confirm button - a
    // sheet with blanks, and each blank cycles through what is installed when you touch it.
    //
    // Godot-free: what a blank offers and what it lands on are questions about content, and the
    // paper only draws the answer. It means the whole of character creation can be walked headless,
    // which is the only way "start a fight and confirm the pool is built from what you wrote" can
    // be checked by anything but a person.
    public sealed class Filling
    {
        readonly Dictionary<Line, List<string>> _offers = new Dictionary<Line, List<string>>();

        public Filling(IEnumerable<ClassCard> classes, IEnumerable<TraitCard> cards)
        {
            _offers[Line.Class] = Ids(classes?.Select(c => c.Id));

            var traits = (cards ?? Array.Empty<TraitCard>()).ToArray();

            _offers[Line.Race] = Ids(traits.Where(c => c.Fills == Blank.Race).Select(c => c.Id));
            _offers[Line.Background] =
                Ids(traits.Where(c => c.Fills == Blank.Background).Select(c => c.Id));
        }

        // sorted, because a dropdown whose order depends on the filesystem walk is a dropdown that
        // reorders itself between machines
        static List<string> Ids(IEnumerable<string> ids) =>
            (ids ?? Array.Empty<string>()).OrderBy(i => i, StringComparer.Ordinal).ToList();

        public IReadOnlyList<string> Offers(Line line) =>
            _offers.TryGetValue(line, out List<string> ids)
                ? ids
                : (IReadOnlyList<string>)Array.Empty<string>();

        public bool CanFill(Line line) => Offers(line).Count > 0;

        // every line that has something to choose from; the rest are written in by hand
        public IEnumerable<Line> Dropdowns =>
            Enum.GetValues<Line>().Where(l => l.IsADropdown() && CanFill(l));


        // what is written on that line now; empty for a blank one
        public static string On(CharacterSheet sheet, Line line) => line switch
        {
            Line.Class => sheet?.ClassId ?? "",
            Line.Race => sheet?.RaceId ?? "",
            Line.Background => sheet?.BackgroundId ?? "",
            Line.Name => sheet?.Name ?? "",
            Line.Appearance => sheet?.Appearance ?? "",
            _ => "",
        };

        public static void Write(CharacterSheet sheet, Line line, string id)
        {
            if (sheet == null) return;

            switch (line)
            {
                case Line.Class: sheet.ClassId = id ?? ""; break;
                case Line.Race: sheet.RaceId = id ?? ""; break;
                case Line.Background: sheet.BackgroundId = id ?? ""; break;
                case Line.Name: sheet.Name = id ?? ""; break;
                case Line.Appearance: sheet.Appearance = id ?? ""; break;
            }
        }

        // Touch a blank and it goes to the next thing on the list; past the end it comes back to
        // empty, so nothing is ever unpickable and there is no Confirm button to reach for.
        //
        // The CLASS line is the exception: a sheet with no class is not a character, so once one is
        // written the line cycles among the classes and never back through blank.
        public string Next(CharacterSheet sheet, Line line)
        {
            IReadOnlyList<string> offers = Offers(line);

            if (offers.Count == 0) return On(sheet, line);

            string standing = On(sheet, line);
            int at = At(offers, standing);

            string next = at < 0 ? offers[0]
                        : at + 1 < offers.Count ? offers[at + 1]
                        : line == Line.Class ? offers[0]
                        : "";

            Write(sheet, line, next);

            return next;
        }

        public string Back(CharacterSheet sheet, Line line)
        {
            IReadOnlyList<string> offers = Offers(line);

            if (offers.Count == 0) return On(sheet, line);

            string standing = On(sheet, line);
            int at = At(offers, standing);

            string back = at < 0 ? offers[offers.Count - 1]
                        : at - 1 >= 0 ? offers[at - 1]
                        : line == Line.Class ? offers[offers.Count - 1]
                        : "";

            Write(sheet, line, back);

            return back;
        }

        // IReadOnlyList has no IndexOf, and the ordinal comparison is the one that matters anyway
        static int At(IReadOnlyList<string> offers, string id)
        {
            for (int at = 0; at < offers.Count; at++)
                if (string.Equals(offers[at], id, StringComparison.Ordinal)) return at;

            return -1;
        }

        // a sheet the DM would accept: it names a class, and every id on it is installed
        public bool Finished(CharacterSheet sheet) =>
            sheet != null
            && !sheet.IsBlank
            && sheet.Filled().All(Installed);

        public bool Installed(string id) =>
            id.Length == 0 || _offers.Values.Any(o => o.Contains(id, StringComparer.Ordinal));

        // what is on the sheet that this shelf no longer has - a pack was unsubscribed, and the
        // sheet still says what it said. Named rather than silently blanked.
        public IEnumerable<string> Missing(CharacterSheet sheet) =>
            (sheet?.Filled() ?? Array.Empty<string>()).Where(id => !Installed(id));

        public override string ToString() =>
            string.Join(", ", _offers.Select(o => $"{o.Key.Word()} x{o.Value.Count}"));
    }
}

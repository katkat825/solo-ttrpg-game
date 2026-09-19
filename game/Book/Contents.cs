using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Book
{
    // WHAT IS PRINTED ON A BOOK'S CONTENTS PAGE, IN THE ORDER IT IS PRINTED.
    //
    // Derived from the two enums rather than listed, so a page added to the campaign book or a
    // chapter added to the rules book appears in the contents the moment it exists and fails the
    // locale audit until somebody has written the line for it. The same argument as EngineKeys:
    // derived, never listed.
    public static class Contents
    {
        public static IReadOnlyList<string> Of(Tome tome) =>
            tome == Tome.Rules
                ? Enum.GetValues<Reference>().Select(References.TitleKey).ToArray()
                : Enum.GetValues<Page>().Select(Pages.NameKey).ToArray();

        public static int Count(Tome tome) => Of(tome).Count;

        // the whole of both books' printed lines, for whatever has to hold them to a locale
        public static IEnumerable<string> Keys()
        {
            foreach (string key in Tomes.Keys()) yield return key;

            foreach (string key in Pages.Keys()) yield return key;

            foreach (string key in References.Keys()) yield return key;
        }
    }
}

using System.Collections.Generic;
using Core.Characters;

namespace Core.Tests
{
    // every test gets its actors through IArchetypeSource, never from a concrete roster.
    //
    // this file is the whole point: when statblocks move into campaign data, the source below
    // changes and not one test does. before 2026-08-20 the tests made 36 static calls straight
    // to BuiltInArchetypes, so the seam existed but nothing used it and the move to data would
    // have rewritten the entire suite.
    //
    // a fresh source per call because Create is documented to hand back a new instance and
    // actors are mutable - a shared one would let a damaged hero leak between tests
    static class Fixtures
    {
        public static IArchetypeSource Archetypes => new BuiltInArchetypes();

        public static Actor Hero() => Archetypes.Create(EngineIds.Barbarian);

        // ordinal 0 means unnumbered, matching Actor.Ordinal
        public static Actor Mook(int ordinal = 0)
        {
            var mook = Archetypes.Create(EngineIds.Rabble);
            return ordinal > 0 ? mook.Numbered(ordinal) : mook;
        }

        public static Actor Rival() => Archetypes.Create(EngineIds.Rival);

        public static IList<Actor> StandardEncounter(int rabbleCount = 4) =>
            Archetypes.Standard(rabbleCount);
    }
}

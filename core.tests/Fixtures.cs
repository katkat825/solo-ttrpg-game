using System.Collections.Generic;
using Core.Characters;

namespace Core.Tests
{
    // fresh source per call: actors are mutable, so a shared one leaks a damaged hero between tests
    static class Fixtures
    {
        public static IArchetypeSource Archetypes => new BuiltInArchetypes();

        public static Actor Hero() => Archetypes.Create(EngineIds.Barbarian);

        // ordinal 0 means unnumbered, matching actor.ordinal
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

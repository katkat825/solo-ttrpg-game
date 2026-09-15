using System.Collections.Generic;

namespace Core.Characters
{
    public static class Encounters
    {
        public static IList<Actor> Standard(this IArchetypeSource archetypes, int rabbleCount = 4)
        {
            var foes = new List<Actor>();

            for (int i = 0; i < rabbleCount; i++)
                foes.Add(archetypes.Create(EngineIds.Rabble).Numbered(i + 1));

            foes.Add(archetypes.Create(EngineIds.Rival));

            return foes;
        }
    }

    public static class BossFight
    {
        public static IList<Actor> WithRabble(this IArchetypeSource archetypes, int rabbleCount = 2)
        {
            var foes = new List<Actor>();

            for (int i = 0; i < rabbleCount; i++)
                foes.Add(archetypes.Create(EngineIds.Rabble).Numbered(i + 1));

            foes.Add(archetypes.Create(EngineIds.Dread));

            return foes;
        }
    }

    // stable ids - renaming one breaks every locale file and save that named it
    public static class EngineIds
    {
        public const string Barbarian = "barbarian";

        public const string Rabble = "rabble";

        public const string Rival = "rival";

        public const string Mage = "mage";

        public const string Dread = "dread";
    }
}

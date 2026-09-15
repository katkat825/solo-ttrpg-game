using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Combat
{
    public interface ITargetSelector
    {
        Actor Choose(Actor attacker, IReadOnlyList<Actor> candidates);
    }

    public sealed class RabbleFirstSelector : ITargetSelector
    {
        public static readonly RabbleFirstSelector Instance = new RabbleFirstSelector();

        public Actor Choose(Actor attacker, IReadOnlyList<Actor> candidates) =>
            candidates
                .Where(c => !c.IsDown)
                .OrderBy(c => c.Tier == Tier.Rabble ? 0 : 1)
                .ThenBy(c => c.Vigor)
                .FirstOrDefault();
    }

    public sealed class StrongestFirstSelector : ITargetSelector
    {
        public Actor Choose(Actor attacker, IReadOnlyList<Actor> candidates) =>
            candidates
                .Where(c => !c.IsDown)
                .OrderByDescending(c => c.Vigor)
                .FirstOrDefault();
    }
}

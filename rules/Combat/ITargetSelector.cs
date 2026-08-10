using System.Collections.Generic;
using System.Linq;
using Rules.Characters;

namespace Rules.Combat
{
    // who gets attacked, and the default answers
    // targeting is policy, not rules - it changes per enemy, per campaign, per boss
    // so swapping it must never mean editing the combat loop
    public interface ITargetSelector
    {
        // null when nothing is worth attacking
        Actor Choose(Actor attacker, IReadOnlyList<Actor> candidates);
    }

    // clear the Rabble, then the weakest Rival
    // cheap, good enough for balance work and a sane hero auto-target
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

    // always the toughest thing standing - for enemies that should be reckless
    public sealed class StrongestFirstSelector : ITargetSelector
    {
        public Actor Choose(Actor attacker, IReadOnlyList<Actor> candidates) =>
            candidates
                .Where(c => !c.IsDown)
                .OrderByDescending(c => c.Vigor)
                .FirstOrDefault();
    }
}

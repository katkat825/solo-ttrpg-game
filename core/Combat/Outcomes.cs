using Core.Characters;
using Core.Resolution;

namespace Core.Combat
{
    public sealed class AttackOutcome
    {
        public Actor Attacker { get; }
        public Actor Target { get; }
        public PoolResult Roll { get; }
        public bool Hit { get; }
        public int Damage { get; }

        public AttackOutcome(Actor attacker, Actor target, PoolResult roll, bool hit, int damage)
        {
            Attacker = attacker;
            Target = target;
            Roll = roll;
            Hit = hit;
            Damage = damage;
        }

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            $"{Attacker.DebugName} -> {Target.DebugName}: {Roll} => " +
            (Hit ? $"HIT for {Damage}" : "miss");
    }

    public sealed class EncounterResult
    {
        public bool HeroWon { get; }
        public int Rounds { get; }
        public int HeroVigorRemaining { get; }

        public EncounterResult(bool won, int rounds, int vigor)
        {
            HeroWon = won;
            Rounds = rounds;
            HeroVigorRemaining = vigor;
        }

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            $"{(HeroWon ? "victory" : "defeat")} in {Rounds} rounds, {HeroVigorRemaining} vigor left";
    }
}

using Content.Kits;
using Core.Characters;
using Core.Resolution;
using Core.Space;
using Game.Tray;

namespace Game.Board
{
    public static class DoorCheck
    {
        public static Ability Ability => SharedKit.ForceDoor;

        public static Attr Attribute => Ability.Attribute;

        public static Skill Skill => Ability.Skill;

        public static int Against => Ability.Against;

        public static Pool PoolFor(Actor hero) => Ability.PoolFor(hero);

        // the shipped row opens the door on a miss too: a check that left it shut would be a repeated scene
        public static DoorOutcome Read(TrayThrow thrown)
        {
            if (thrown == null) return new DoorOutcome(false, Tile.Floor, 0, 0);

            AbilityOutcome outcome = Ability.Read(thrown.Result, thrown.ImpactValue);

            return new DoorOutcome(
                outcome.Does(Effect.Open) && outcome.Landed,

                // wreckage: double to cross, for the rest of the game
                outcome.Does(Effect.Rough) ? Tile.Rough : Tile.Floor,

                outcome.Total,

                // recoil read off the felt, never rolled again
                outcome.Does(Effect.Recoil) ? outcome.Magnitude : 0);
        }
    }

    public readonly struct DoorOutcome
    {
        public readonly bool Forced;

        public readonly Tile Leaves;

        public readonly int Total;

        public readonly int Cost;

        public DoorOutcome(bool forced, Tile leaves, int total, int cost)
        {
            Forced = forced;
            Leaves = leaves;
            Total = total;
            Cost = cost;
        }

        // developer only, not localized, must never reach the screen
        public override string ToString() =>
            Forced
                ? $"forced, {Total} vs {DoorCheck.Against}"
                : $"gave way, {Total} vs {DoorCheck.Against} - {Cost} vigor and wreckage through it";
    }
}

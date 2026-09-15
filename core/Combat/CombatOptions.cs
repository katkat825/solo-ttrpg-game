using Core.Characters;

namespace Core.Combat
{
    public sealed class CombatOptions
    {
        // the hero acts more than everyone - one action a round measures at a 4% win rate
        public int HeroActionsPerRound { get; set; } = 2;

        public int HeroReactionsPerRound { get; set; } = 1;

        public bool ImpactExplodes { get; set; } = true;   // pacing, not power - shortens fights ~15%

        public int MaxRounds { get; set; } = 40;           // safety net so a bad change can't hang the sim

        public int Pace { get; set; } = 5;

        // off makes the player path issue Run's calls in Run's order, so the two match to the decimal (a faithfulness test)
        public bool RollInitiative { get; set; } = true;

        public Attr HeroAttackAttr { get; set; } = Attr.Might;
        public Skill HeroAttackSkill { get; set; } = Skill.Blades;
    }
}

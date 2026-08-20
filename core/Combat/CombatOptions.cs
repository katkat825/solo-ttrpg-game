using Core.Characters;

namespace Core.Combat
{
    // the dials a fight is run with
    // defaults are the tuned ones - change any of them and re-run the sim
    // nothing in here is content, so a campaign must not be able to set it
    public sealed class CombatOptions
    {
        // the hero acts more than everyone else - this is the action-economy fix
        // one action per round measures at a 4% win rate
        public int HeroActionsPerRound { get; set; } = 2;

        public bool ImpactExplodes { get; set; } = true;   // pacing, not power - shortens fights ~15%

        public int MaxRounds { get; set; } = 40;           // safety net so a bad change can't hang the sim

        public Attr HeroAttackAttr { get; set; } = Attr.Might;
        public Skill HeroAttackSkill { get; set; } = Skill.Blades;
    }
}

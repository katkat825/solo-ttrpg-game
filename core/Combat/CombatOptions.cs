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

        // AND ONE REACTION, which is the other half of "the hero is a person who acts more"
        // (CORE_RULES.md section 8). It is here beside HeroActionsPerRound rather than on the
        // Actor for the same reason that one is: it is a dial on the hero's side of the action
        // economy, and the action economy is the balance lever SIMULATION.md section 2 measured.
        // Tier.ReactionsPerRound is what a DREAD gets, which is a property of being a Dread
        public int HeroReactionsPerRound { get; set; } = 1;

        public bool ImpactExplodes { get; set; } = true;   // pacing, not power - shortens fights ~15%

        public int MaxRounds { get; set; } = 40;           // safety net so a bad change can't hang the sim

        // HOW FAR ONE MOVE CARRIES A PIECE, in squares, counted the way Route counts them -
        // diagonals are one and difficult ground is two (Tile.MoveCost).
        //
        // Phase B deliberately had no reachability limit at all: one click walked a piece
        // anywhere it could reach, "no turns, no whose-move-is-it - that is COMBAT_LOOP.md".
        // This is COMBAT_LOOP.md, and a fight where everybody can cross the room every turn is a
        // fight where position means nothing and Rabble cannot create the pressure CORE_RULES.md
        // section 8 built them for.
        //
        // FIVE, because it is half the width of the shipped room - so closing on somebody across
        // the hall costs a turn, and the hero can disengage from one Rabble and reach another.
        // It is a dial and not a constant precisely so it can be simulated when a report exists
        // that gives anyone a position; nothing in SIMULATION.md has geometry in it yet, which is
        // also why no flanking bonus and no cover modifier were added with it (COMBAT_LOOP.md C2:
        // decide deliberately and simulate, rather than adding a modifier by feel)
        public int Pace { get; set; } = 5;

        // WHETHER THE ORDER IS THROWN FOR (CORE_RULES.md section 8) or is simply the order
        // everybody was mustered in.
        //
        // On is the game. Off exists for one reason and it is a good one: `CombatEngine.Run` has
        // no initiative in it - it takes the hero's whole turn and then every foe's - and it is
        // what every number in SIMULATION.md was measured with. With this off, the player-driven
        // path makes exactly Run's calls in exactly Run's order and the two agree to the last
        // decimal, which is the property `sim/Reports/PlayerPathReport.cs` exists to hold. With it
        // on they cannot, because one extra throw at the top moves every seeded number after it.
        //
        // So the two questions are asked separately: is the path faithful (off), and what does
        // initiative cost (on against off). That is CONVENTIONS.md 8 applied to a rule that had
        // never been simulated because nothing had ever implemented it
        public bool RollInitiative { get; set; } = true;

        public Attr HeroAttackAttr { get; set; } = Attr.Might;
        public Skill HeroAttackSkill { get; set; } = Skill.Blades;
    }
}

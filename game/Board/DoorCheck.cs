using Core.Characters;
using Core.Resolution;
using Core.Space;
using Game.Tray;

namespace Game.Board
{
    // THE ONE CHECK ON THE BOARD, AND THE ONLY HARDCODED CONTENT IN THIS PHASE.
    //
    // A shut door, a hero with an axe, and one throw that answers both "did it work" and "how
    // well". Everything here - which attribute, which skill, how hard, and what pass and fail
    // leave behind - is campaign content that Phase P will read out of a data file, and
    // THE_BOARD.md B4 says to hardcode it until then. It is in a file of its own so that swap is
    // a deletion rather than an excavation, and it is Godot-free so the outcome rules are
    // testable without an engine.
    //
    // FAILURE IS CONTENT, NOT A WALL (CORE_RULES.md 0, pillar 4): "a failed roll should produce a
    // different scene, not a repeated one". So a failed check does NOT leave the door shut for
    // you to click again - that is the repeated scene the pillar forbids, and a lock you retry
    // until the dice go your way is a lock that was never a decision. You get through either way.
    // What the throw decides is HOW, and the board keeps the answer:
    //
    //   forced   the door swings open and the way through is clear
    //   given    it comes off its hinges in pieces, which land in the room BEYOND as difficult
    //            ground - double cost to cross, for the rest of the game - and the hero wears the
    //            Impact die
    //
    // That is one line to invert if the door should hold instead - Leaves and Cost below are the
    // whole of what a failure means.
    //
    // SINCE EDGE_WALLS.md A DOOR IS A LINE, so the wreckage has somewhere to land: both squares
    // beside it are floor. When the door was a square of its own, the only thing a failure could
    // make difficult was the doorway itself.
    public static class DoorCheck
    {
        // Might, because this is a shoulder and an axe rather than a lockpick, and Blades because
        // CORE_RULES calls an axe a blade. The barbarian is TRAINED in it, which is what keeps the
        // pool at three dice and the tray full - an untrained attempt is a real thing the rules
        // handle and a tray that can sit a die out is presentation work nobody has done yet
        public const Attr Attribute = Attr.Might;

        public const Skill Skill = Core.Characters.Skill.Blades;

        // "a competent person is trying a real task" - CORE_RULES 5. The starting hero beats it
        // about 64% of the time, so both outcomes turn up in an evening of clicking
        public const int Against = Difficulty.Standard;

        // what the hero throws at it. exactly Actor.BuildPool - attribute, skill, gear - so the
        // dice on the felt are the hero's own and the marks name the traits that earned them
        public static Pool PoolFor(Actor hero) => hero?.BuildPool(Attribute, Skill);

        // what the felt says happened to the door
        public static DoorOutcome Read(TrayThrow thrown)
        {
            if (thrown == null) return new DoorOutcome(false, Tile.Floor, 0, 0);

            bool forced = thrown.Result.Beats(Against);

            return new DoorOutcome(
                forced,
                forced ? Tile.Floor : Tile.Rough,
                thrown.Result.Total,

                // the door comes back at you. read off the felt, never rolled again - the
                // magnitude is the die already lying there with a ring round it
                forced ? 0 : thrown.ImpactValue);
        }
    }

    // one answer, in the only three parts the board needs
    public readonly struct DoorOutcome
    {
        // did the hero take the door cleanly
        public readonly bool Forced;

        // what the room beyond is left as - Floor for nothing at all, Rough for the wreckage of a
        // door lying in it
        public readonly Tile Leaves;

        // the two counted dice, for the log
        public readonly int Total;

        // what it cost the hero in Vigor, which is 0 on a clean break
        public readonly int Cost;

        public DoorOutcome(bool forced, Tile leaves, int total, int cost)
        {
            Forced = forced;
            Leaves = leaves;
            Total = total;
            Cost = cost;
        }

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            Forced
                ? $"forced, {Total} vs {DoorCheck.Against}"
                : $"gave way, {Total} vs {DoorCheck.Against} - {Cost} vigor and wreckage through it";
    }
}

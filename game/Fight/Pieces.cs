using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Game.Board;

namespace Game.Fight
{
    // WHO IS WHO. One combatant as it exists on this table: the rules' Actor, the model standing
    // on a square, and the tally of Vigor beside it.
    //
    // The rules narrate in Actors and the board is made of Minis, and something has to hold the
    // two together. That something is here rather than inside the observer, because the fight
    // needs the same lookup to turn a CLICK into a target - a map kept in two places is two maps
    // that can disagree about who the piece on (4, 1) is.
    //
    // Nothing here is a rule. It knows nothing about turns, damage or targeting; it answers
    // "which model is this Actor" and "which Actor is this model" and stops.
    public sealed class Piece
    {
        public Actor Actor { get; }

        public Mini Mini { get; }

        // null for a Rabble, which has no health track and therefore nothing to tally
        // (CORE_RULES.md section 8). A piece with no pips beside it is information
        public VigorPips Vigor { get; }

        // what is wrong with it, written on the mat beside it (C1)
        public ConditionMarks Marks { get; }

        // WHICH SPAWN SLOT IT WAS PUT ON (P2), and 0 for a piece the scene placed by square or for
        // the hero. A save needs a stable name for each foe and neither of the obvious ones works:
        // an id repeats (four hounds), and a square moves. The slot the encounter placed it on is
        // the one thing about a foe that is unique and never changes, and it is also what a person
        // hand-editing a save would recognise - "the one that started on spawn 2" (P6)
        public int Slot { get; }

        public Piece(Actor actor, Mini mini, VigorPips vigor, ConditionMarks marks, int slot = 0)
        {
            Actor = actor;
            Mini = mini;
            Vigor = vigor;
            Marks = marks;
            Slot = slot;
        }

        public bool IsDown => Actor.IsDown;

        // DEVELOPER ONLY - not localized, never reaches the screen
        public override string ToString() => $"{Actor.DebugName} as {Mini?.Name}";
    }

    public sealed class Pieces
    {
        // reference identity on the Actor, the same choice Core.Space.Grid makes about occupants
        // and for the same reason: two actors that consider themselves equal are still two actors
        readonly Dictionary<Actor, Piece> _byActor =
            new Dictionary<Actor, Piece>(ReferenceEqualityComparer.Instance);

        readonly Dictionary<Mini, Piece> _byMini =
            new Dictionary<Mini, Piece>(ReferenceEqualityComparer.Instance);

        readonly List<Piece> _order = new List<Piece>();

        // in the order they were mustered, which is the order the fight lists them in
        public IReadOnlyList<Piece> All => _order;

        public Piece Add(Actor actor, Mini mini, VigorPips vigor, ConditionMarks marks,
                         int slot = 0)
        {
            if (actor == null || mini == null) return null;

            var piece = new Piece(actor, mini, vigor, marks, slot);

            _byActor[actor] = piece;
            _byMini[mini] = piece;
            _order.Add(piece);

            return piece;
        }

        public Piece Of(Actor actor) =>
            actor != null && _byActor.TryGetValue(actor, out Piece piece) ? piece : null;

        public Piece On(Mini mini) =>
            mini != null && _byMini.TryGetValue(mini, out Piece piece) ? piece : null;

        public IEnumerable<Piece> Standing => _order.Where(p => !p.IsDown);
    }
}

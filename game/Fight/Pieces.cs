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

        public Piece(Actor actor, Mini mini, VigorPips vigor, ConditionMarks marks)
        {
            Actor = actor;
            Mini = mini;
            Vigor = vigor;
            Marks = marks;
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

        public Piece Add(Actor actor, Mini mini, VigorPips vigor, ConditionMarks marks)
        {
            if (actor == null || mini == null) return null;

            var piece = new Piece(actor, mini, vigor, marks);

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

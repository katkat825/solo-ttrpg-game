using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Game.Board;

namespace Game.Fight
{
    public sealed class Piece
    {
        public Actor Actor { get; }

        public Mini Mini { get; }

        // null for a Rabble, which has no health track
        public VigorPips Vigor { get; }

        public ConditionMarks Marks { get; }

        // stable save id for a foe; 0 for the hero or a square-placed piece
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

        // developer only, not localized, never reaches the screen
        public override string ToString() => $"{Actor.DebugName} as {Mini?.Name}";
    }

    public sealed class Pieces
    {
        // reference identity: two actors that consider themselves equal are still two actors
        readonly Dictionary<Actor, Piece> _byActor =
            new Dictionary<Actor, Piece>(ReferenceEqualityComparer.Instance);

        readonly Dictionary<Mini, Piece> _byMini =
            new Dictionary<Mini, Piece>(ReferenceEqualityComparer.Instance);

        readonly List<Piece> _order = new List<Piece>();

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

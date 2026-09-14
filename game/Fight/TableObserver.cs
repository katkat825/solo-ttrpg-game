using Godot;
using Core.Characters;
using Core.Combat;
using Core.Space;
using BoardNode = Game.Board.Board;

namespace Game.Fight
{
    // THE VIEW OF A FIGHT, AND THE ONLY SEAM IT NEEDS (COMBAT_LOOP.md, architecture notes).
    //
    // `ICombatObserver` is how the rules narrate, and this is Godot listening. Nothing in `core/`
    // learns that anything is drawing it: `CombatEngine` says a swing landed and an actor went
    // down, and what that LOOKS like - which model leans at which, whose tally goes out, which
    // piece is laid on its side - is decided here and nowhere else.
    //
    // It is a plain class rather than a Node on purpose. It owns no clock and no children; it is
    // handed a board and a roster and tells pieces what to do. `Fight` owns it, `Fight` owns the
    // engine, and the composition happens in one place.
    //
    // IT DRAWS AND DOES NOT NARRATE. `Fight` runs a `RecordingCombatObserver` beside this one, so
    // the transcript is written once, by the thing whose job that is - and the C0 verify ("attach
    // a recorder alongside and confirm the transcript matches what you saw") is then two
    // independent readings of the same events rather than one printed twice. The only lines here
    // are ones the recorder cannot know, because they are about the table: which square a piece
    // fell on. Those are developer diagnostics and exempt from localization, exactly as the ones
    // in `DiceTray` and `Board` are. Nothing this class writes reaches a player - what a player
    // reads of a fight is the pieces and the felt.
    public sealed class TableObserver : ICombatObserver
    {
        readonly BoardNode _board;

        readonly Pieces _pieces;

        public TableObserver(BoardNode board, Pieces pieces)
        {
            _board = board;
            _pieces = pieces;
        }

        // C3 draws the turn order on the table, and that is where a round becomes something the
        // player can read. The transcript says it in the meantime
        public void RoundBegan(int round)
        {
        }

        // and the same for whose turn it is. The table already says it in the only way that
        // matters at C2 - the foes move and swing by themselves, and when they stop, it is yours
        public void TurnBegan(Actor actor, int round)
        {
        }

        public void AttackResolved(AttackOutcome outcome)
        {
            Piece attacker = _pieces.Of(outcome.Attacker);
            Piece target = _pieces.Of(outcome.Target);

            // the swing itself: reach at the thing and come back. it happens on a miss too,
            // because a miss is a swing that missed and not an absence of one
            if (attacker != null && target != null)
                attacker.Mini.Strike(target.Mini.Position);

            // the tally is the damage, seen. it is refreshed rather than decremented, so the
            // pips are a view of Actor.Vigor and can never drift from it
            target?.Vigor?.Show(outcome.Target.Vigor);
        }

        // THE MARK IS ALREADY COMING - Fight refreshes the mat from the Actor every frame, so a
        // Condition shows up whoever applied it. What only this can know is that it is NEW rather
        // than merely still true, and that is the flare
        public void ConditionApplied(Actor actor, Condition condition)
        {
            _pieces.Of(actor)?.Marks?.Flash(condition);
        }

        public void ActorDowned(Actor actor)
        {
            Piece piece = _pieces.Of(actor);

            if (piece == null) return;

            // OFF THE GRID BUT NOT OFF THE TABLE. The square is free for anybody to walk onto -
            // the rules have finished with this actor - but the model is laid on its side and
            // left where it fell, which is what a table looks like and is worth more than tidiness
            Cell? fell = _board?.CellOf(piece.Mini);

            _board?.Squares.Remove(piece.Mini);
            piece.Mini.Topple();

            // the tally and the marks go with it: an empty tally and a list of complaints beside
            // a body are sums nobody is keeping any more
            if (piece.Vigor != null) piece.Vigor.Visible = false;
            if (piece.Marks != null) piece.Marks.Visible = false;

            GD.Print($"        {actor.DebugName} goes down" + (fell == null ? "" : $" on {fell}"));
        }

        // A BOSS BECOMING THE SECOND HALF OF THE FIGHT (CORE_RULES.md section 8). The dice have
        // already re-rated by the time this fires, so what is left is to make the moment land: the
        // piece rears up and settles, which is the same movement a swing uses because it is the
        // same gesture - a hand picking a model up and putting it down harder.
        //
        // The change itself is watchable without any of this, and deliberately: the next handful
        // the boss throws is built from bigger dice, and its tally is at half. This is only the
        // punctuation
        public void PhaseChanged(Actor actor)
        {
            Piece piece = _pieces.Of(actor);

            if (piece == null) return;

            piece.Mini.Strike(piece.Mini.Position + Vector3.Up * 0.02f);
        }

        // THE TOKENS ARE ALREADY COMING - Fight refreshes them from the Actor every frame, the way
        // it refreshes the tally and the marks, so a Nerve spent or banked anywhere shows on the
        // table without its source having to know this exists
        public void NerveChanged(Actor actor, int was, int now)
        {
        }

        // nothing to draw. The table already says it: one side is standing and the other is
        // lying down, which is the only end screen a tabletop has ever had
        public void EncounterEnded(EncounterResult result)
        {
        }
    }
}

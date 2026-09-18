using Godot;
using Core.Characters;
using Core.Combat;
using Core.Space;
using BoardNode = Game.Board.Board;

namespace Game.Fight
{
    public sealed class TableObserver : ICombatObserver
    {
        readonly BoardNode _board;

        readonly Pieces _pieces;

        // optional, like everywhere else the companion appears: a fight with nobody beside the map
        // plays exactly as it did, and the fallen still go
        readonly Game.Companion.Companion _friend;

        public TableObserver(BoardNode board, Pieces pieces,
                             Game.Companion.Companion friend = null)
        {
            _board = board;
            _pieces = pieces;
            _friend = friend;
        }

        public System.Action<Actor> Loot { get; set; }

        public void RoundBegan(int round)
        {
        }

        public void TurnBegan(Actor actor, int round)
        {
        }

        public void AttackResolved(AttackOutcome outcome)
        {
            Piece attacker = _pieces.Of(outcome.Attacker);
            Piece target = _pieces.Of(outcome.Target);

            // happens on a miss too: a miss is a swing that missed
            if (attacker != null && target != null)
                attacker.Mini.Strike(target.Mini.Position);

            // refreshed from Actor.Vigor, never decremented, so the pips cannot drift
            target?.Vigor?.Show(outcome.Target.Vigor);
        }

        // the mark comes from Fight's per-frame refresh; only this knows it is new, so only the flare
        public void ConditionApplied(Actor actor, Condition condition)
        {
            _pieces.Of(actor)?.Marks?.Flash(condition);

            _pieces.Of(actor)?.Mini?.Wobble();
        }

        public void ActorDowned(Actor actor)
        {
            Piece piece = _pieces.Of(actor);

            if (piece == null) return;

            // freed from the grid, toppled where it fell, and then cleared off the table: the
            // fall is the beat and the body is not scenery (the eye check, 2026-09-16)
            Cell? fell = _board?.CellOf(piece.Mini);

            _board?.Squares.Remove(piece.Mini);
            piece.Mini.Topple();

            // and the companion is what takes it. It huffs, and the thing skitters away from IT -
            // which is why the creature's position is what the piece is shooed from. With nobody
            // at the table it still goes, straight back off the near edge.
            _friend?.HuffsAtTheFallen(piece.Mini);

            piece.Mini.Skitter(_friend?.GlobalPosition ?? piece.Mini.GlobalPosition + Vector3.Back);

            if (piece.Vigor != null) piece.Vigor.Visible = false;
            if (piece.Marks != null) piece.Marks.Visible = false;

            Loot?.Invoke(actor);

            GD.Print($"        {actor.DebugName} goes down" + (fell == null ? "" : $" on {fell}"));
        }

        // the phase change already took effect in the rules; this is only the visual punctuation
        public void PhaseChanged(Actor actor)
        {
            Piece piece = _pieces.Of(actor);

            if (piece == null) return;

            piece.Mini.Strike(piece.Mini.Position + Vector3.Up * 0.02f);
        }

        public void NerveChanged(Actor actor, int was, int now)
        {
        }

        public void EncounterEnded(EncounterResult result)
        {
        }
    }
}

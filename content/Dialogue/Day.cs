using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Combat;

namespace Content.Dialogue
{
    // What happened today, which is what camp is about (CORE_RULES.md section 11, W3). A camp
    // conversation that opens with "so, nothing much" on the night you killed the boss is the
    // moment the companion stops being a character, so the night reads the day rather than a menu.
    //
    // It is an ICombatObserver, so it learns the day by watching the same fights the table does -
    // no second source of truth, and nothing in core/ has to know a companion exists.
    public sealed class Day : ICombatObserver
    {
        readonly Actor _hero;

        public Day(Actor hero) => _hero = hero;

        public int Fights { get; private set; }

        public int Won { get; private set; }

        public int Felled { get; private set; }

        // a Dread went down today. One per chapter, so this is a night with something to say
        public bool KilledABoss { get; private set; }

        // the hero hit the floor, or a phase change nearly did it
        public bool WentDown { get; private set; }

        public int NerveSpent { get; private set; }

        // Conditions taken today, in the order they landed
        public IReadOnlyList<Condition> Took => _took;

        readonly List<Condition> _took = new List<Condition>();

        // the worst the hero's Vigor got, as a fraction of its maximum; 1.0 for an untouched day
        public double Lowest { get; private set; } = 1.0;

        public bool Bloodied => Lowest <= Half;

        public const double Half = 0.5;

        public bool Untouched => Fights > 0 && Lowest >= 1.0 && _took.Count == 0;

        public bool Quiet => Fights == 0;


        public void RoundBegan(int round) { }

        public void TurnBegan(Actor actor, int round) { }

        public void AttackResolved(AttackOutcome outcome)
        {
            if (outcome?.Target == null) return;

            // read after the blow lands, so the low-water mark is the number that frightened you
            if (ReferenceEquals(outcome.Target, _hero)) Mark();
        }

        public void ConditionApplied(Actor actor, Condition condition)
        {
            if (ReferenceEquals(actor, _hero)) _took.Add(condition);
        }

        public void ActorDowned(Actor actor)
        {
            if (actor == null) return;

            if (ReferenceEquals(actor, _hero)) { WentDown = true; Mark(); return; }

            Felled++;

            if (actor.Tier == Tier.Dread) KilledABoss = true;
        }

        public void NerveChanged(Actor actor, int was, int now)
        {
            if (ReferenceEquals(actor, _hero) && now < was) NerveSpent += was - now;
        }

        public void PhaseChanged(Actor actor) { }

        public void EncounterEnded(EncounterResult result)
        {
            Fights++;

            if (result != null && result.HeroWon) Won++;

            Mark();
        }

        void Mark()
        {
            if (_hero == null || _hero.MaxVigor <= 0) return;

            double now = (double)_hero.Vigor / _hero.MaxVigor;

            if (now < Lowest) Lowest = now;
        }

        // the night starts over; a day's damage follows you but a day's conversation does not
        public void Slept()
        {
            Fights = 0;
            Won = 0;
            Felled = 0;
            KilledABoss = false;
            WentDown = false;
            NerveSpent = 0;
            _took.Clear();
            Lowest = 1.0;
        }


        // worst first, so a night with several answers gets the one worth talking about
        public Topic About()
        {
            if (WentDown) return Topic.Loss;
            if (KilledABoss) return Topic.Boss;
            if (Bloodied) return Topic.Bloodied;
            if (NerveSpent > 0) return Topic.Trouble;
            if (_took.Count > 0 || (_hero != null && _hero.Conditions.Count > 0)) return Topic.Wounded;
            if (Untouched) return Topic.Untouched;

            return Topic.Quiet;
        }

        // Every topic this day could HONESTLY open with, best first. There are only ever two: what
        // happened, and Quiet.
        //
        // The tempting version walks the whole ladder downwards, so a campaign missing a 'boss'
        // scene falls through to 'bloodied' - and then the companion says "you are still bleeding"
        // on a night you took nothing. Every topic but Quiet asserts something about the day, so
        // Quiet is the only safe thing to fall back to, and a campaign that wrote no quiet scene
        // gets silence. Silence is better than a companion who was not paying attention.
        public IEnumerable<Topic> Offers()
        {
            Topic best = About();

            yield return best;

            if (best != Topic.Quiet) yield return Topic.Quiet;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{Fights} fights, {Won} won, {Felled} felled" +
            (KilledABoss ? ", a Dread among them" : "") +
            (WentDown ? ", and the hero on the floor" : "") +
            $"; vigor down to {Lowest:0%}, {NerveSpent} nerve spent, " +
            $"{_took.Count} conditions - {About().Word()}";
    }
}

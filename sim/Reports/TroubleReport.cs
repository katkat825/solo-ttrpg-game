using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Sim
{
    // taking every trouble is the pessimistic read: this auto-player never shrugs, so the cost is an upper bound
    static class TroubleReport
    {
        static readonly IArchetypeSource Archetypes = new BuiltInArchetypes();

        public static void Run(int trials)
        {
            Table.Title("What Trouble costs, now that it has teeth");
            Table.Header("two or more 1s", "win %", "avg rounds", "vigor left");

            Stats without = Sweep(trials, bites: false);
            Stats with = Sweep(trials, bites: true);

            WriteRow("reported only", without);
            WriteRow("gear takes it", with);

            Table.Row("difference",
                Table.Pct(with.WinPct - without.WinPct),
                Table.Num(with.AvgRounds - without.AvgRounds),
                Table.Num(with.AvgVigorLeft - without.AvgVigorLeft));
        }

        readonly struct Stats
        {
            public readonly double WinPct;
            public readonly double AvgRounds;
            public readonly double AvgVigorLeft;

            public Stats(double win, double rounds, double vigor)
            {
                WinPct = win;
                AvgRounds = rounds;
                AvgVigorLeft = vigor;
            }
        }

        static void WriteRow(string label, Stats s) =>
            Table.Row(label, Table.Pct(s.WinPct), Table.Num(s.AvgRounds), Table.Num(s.AvgVigorLeft));

        static Stats Sweep(int trials, bool bites)
        {
            var engine = new CombatEngine(
                new StandardResolver(new SeededRng(4242)),
                new CombatOptions { RollInitiative = false });

            int wins = 0;
            long rounds = 0;
            long vigor = 0;

            for (int i = 0; i < trials; i++)
            {
                Actor hero = Archetypes.Create(EngineIds.Barbarian);

                EncounterResult r = Fight(engine, hero, Archetypes.Standard(), bites);

                rounds += r.Rounds;

                if (r.HeroWon)
                {
                    wins++;
                    vigor += r.HeroVigorRemaining;
                }
            }

            return new Stats(
                100.0 * wins / trials,
                (double)rounds / trials,
                wins > 0 ? (double)vigor / wins : 0);
        }

        // only the hero's troubles are applied; what one costs a monster is undecided
        static EncounterResult Fight(CombatEngine engine, Actor hero, IList<Actor> foes, bool bites)
        {
            var fight = new Encounter(engine, hero, foes);

            fight.Begin();

            while (!fight.IsOver)
            {
                Actor acting = fight.Acting;

                if (ReferenceEquals(acting, hero))
                {
                    Actor target = engine.HeroTargeting.Choose(acting, fight.Foes);

                    if (target == null || fight.ActionsLeft <= 0) { fight.EndTurn(); continue; }

                    AttackOutcome swing = fight.Strike(
                        target, engine.Options.HeroAttackAttr, engine.Options.HeroAttackSkill);

                    if (bites && swing != null && swing.Roll.Trouble)
                        Trouble.Lands(hero, engine.Options.HeroAttackAttr);

                    continue;
                }

                Actor prey = fight.TargetFor(acting);

                if (prey == null || fight.ActionsLeft <= 0) { fight.EndTurn(); continue; }

                fight.Strike(prey, acting.Tier.AttackAttr(), acting.Tier.AttackSkill());
            }

            return fight.Result;
        }
    }
}

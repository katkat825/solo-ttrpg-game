using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Sim
{
    // goes through encounter not run: run can't express two actions a round or a phase change
    static class DreadReport
    {
        static readonly IArchetypeSource Archetypes = new BuiltInArchetypes();

        public static void Run(int trials)
        {
            Table.Title("The chapter boss: Barbarian vs 2 Rabble + 1 Dread");
            Table.Header("boss", "win %", "avg rounds", "vigor left");

            WriteRow("as it ships", Sweep(trials, phases: true, defense: 0));
            WriteRow("no phase change", Sweep(trials, phases: false, defense: 0));

            Table.Title("And what Defense does to it (the scalpel)");
            Table.Header("boss defense", "win %", "avg rounds", "vigor left");

            foreach (int defense in new[] { 10, 11, 12, 13 })
                WriteRow(defense.ToString(), Sweep(trials, phases: true, defense: defense));
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

        static Stats Sweep(int trials, bool phases, int defense)
        {
            // initiative off so every row runs against the same rng sequence
            var engine = new CombatEngine(
                new StandardResolver(new SeededRng(4242)),
                new CombatOptions { RollInitiative = false });

            int wins = 0;
            long rounds = 0;
            long vigor = 0;

            for (int i = 0; i < trials; i++)
            {
                Actor hero = Archetypes.Create(EngineIds.Barbarian);
                IList<Actor> foes = Archetypes.WithRabble();

                if (defense > 0)
                    foreach (Actor foe in foes)
                        if (foe.Tier == Tier.Dread) foe.BaseDefense = defense;

                EncounterResult r = Fight(engine, hero, foes, phases);

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

        static EncounterResult Fight(CombatEngine engine, Actor hero, IList<Actor> foes, bool phases)
        {
            var fight = new Encounter(engine, hero, foes);

            if (phases)
                foreach (Actor foe in foes)
                    if (foe.Tier == Tier.Dread) fight.Phases(PhaseChange.Standard(foe));

            fight.Begin();

            while (!fight.IsOver)
            {
                Actor acting = fight.Acting;

                if (ReferenceEquals(acting, hero))
                {
                    Actor target = engine.HeroTargeting.Choose(acting, fight.Foes);

                    if (target == null || fight.ActionsLeft <= 0) { fight.EndTurn(); continue; }

                    fight.Strike(target, engine.Options.HeroAttackAttr, engine.Options.HeroAttackSkill);
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

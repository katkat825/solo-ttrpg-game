using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Sim
{
    // THE CHAPTER BOSS, MEASURED RATHER THAN FELT (COMBAT_LOOP.md C6, CONVENTIONS.md 8).
    //
    // A Dread is the one fight in a chapter that is meant to be hard, and "hard" is a number
    // somebody has to choose. Nothing in SIMULATION.md covers it, because until C6 nothing in the
    // engine could fight one: `Tier.Dread` looked like it acted twice and did not (SEAMS.md
    // section 3), and there was no phase change at all. So this is the first measurement of it.
    //
    // IT HAS TO GO THROUGH `Encounter` AND NOT `Run`. Two actions a round and a phase change at
    // half Vigor are both things `Run` cannot express - it takes the hero's whole turn and then
    // one swing per foe, and it has no phase registry. That is not a divergence between the two
    // paths, it is a fight `Run` was never able to describe; `PlayerPathReport` still holds the
    // two to the last decimal on every encounter `Run` CAN describe.
    //
    // WHAT TO READ IT FOR. Defense is the scalpel and Vigor is the blunt instrument
    // (SIMULATION.md section 3), so the interesting column is the win rate against Defense and
    // the interesting comparison is with and without the phase change - a second phase that moves
    // the win rate by a point is a second phase nobody will notice.
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
            // initiative off, so every row is measured against the same RNG sequence and the only
            // thing moving between them is the thing being measured
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
                        if (foe.Tier == Tier.Dread) foe.Defense = defense;

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

        // the same auto-player PlayerPathReport uses - a deliberately poor one that never moves,
        // never spends Nerve and never retreats. The question is what the STATBLOCK comes to, not
        // what somebody clever can do with the hero
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

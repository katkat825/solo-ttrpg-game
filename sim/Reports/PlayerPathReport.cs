using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Sim
{
    // initiative is off for the faithfulness run; turning it on is a rules change, measured in the second table
    static class PlayerPathReport
    {
        static readonly IArchetypeSource Archetypes = new BuiltInArchetypes();

        public static void Run(int trials)
        {
            Table.Title("The player-driven path against Run (they must agree)");
            Table.Header("driven by", "win %", "avg rounds", "vigor left");

            Stats scripted = Sweep(trials, played: false, initiative: false);
            Stats played = Sweep(trials, played: true, initiative: false);

            WriteRow("CombatEngine.Run", scripted);
            WriteRow("Encounter", played);
            WriteDifference(scripted, played);

            Table.Title("What initiative costs");
            Table.Header("turn order", "win %", "avg rounds", "vigor left");

            Stats mustered = played;
            Stats rolled = Sweep(trials, played: true, initiative: true);

            WriteRow("hero first", mustered);
            WriteRow("Grace + Insight", rolled);
            WriteDifference(mustered, rolled);
        }

        static void WriteDifference(Stats from, Stats to) =>
            Table.Row(
                from.Same(to) ? "no difference" : "difference",
                Table.Pct(to.WinPct - from.WinPct),
                Table.Num(to.AvgRounds - from.AvgRounds),
                Table.Num(to.AvgVigorLeft - from.AvgVigorLeft));

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

            // exact, not close: same seed and calls is the same fight, so any difference is a rules difference
            public bool Same(Stats other) =>
                WinPct == other.WinPct && AvgRounds == other.AvgRounds && AvgVigorLeft == other.AvgVigorLeft;
        }

        static void WriteRow(string label, Stats s) =>
            Table.Row(label, Table.Pct(s.WinPct), Table.Num(s.AvgRounds), Table.Num(s.AvgVigorLeft));

        static Stats Sweep(int trials, bool played, bool initiative)
        {
            // one seed and one resolver across every trial: the sequence matters as much as the seed
            var engine = new CombatEngine(
                new StandardResolver(new SeededRng(4242)),
                new CombatOptions { RollInitiative = initiative });

            int wins = 0;
            long rounds = 0;
            long vigor = 0;

            for (int i = 0; i < trials; i++)
            {
                Actor hero = Archetypes.Create(EngineIds.Barbarian);

                EncounterResult r = played
                    ? AutoPlay(engine, hero, Archetypes.Standard())
                    : engine.Run(hero, Archetypes.Standard());

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

        // deliberately poor auto-player: run's own choices from outside, to test the path not clever play
        static EncounterResult AutoPlay(CombatEngine engine, Actor hero, System.Collections.Generic.IList<Actor> foes)
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

                    fight.Strike(target, engine.Options.HeroAttackAttr, engine.Options.HeroAttackSkill);
                    continue;
                }

                Actor prey = fight.TargetFor(acting);

                if (prey == null) { fight.EndTurn(); continue; }

                fight.Strike(prey, acting.Tier.AttackAttr(), acting.Tier.AttackSkill());
            }

            return fight.Result;
        }
    }
}

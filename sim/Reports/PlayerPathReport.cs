using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Sim
{
    // DO THE TWO PATHS PLAY THE SAME GAME? (COMBAT_LOOP.md, architecture notes.)
    //
    // `CombatEngine.Run` is what every number in SIMULATION.md was measured with. `Encounter` is
    // what the table actually plays, because a player chooses the hero's actions. Those are two
    // implementations of one round structure, and the note in the build spec is blunt about the
    // risk: "if a change to the player path forces a change to Run's results, the sim's numbers
    // move and SIMULATION.md is now wrong until re-verified."
    //
    // So this report drives `Encounter` with an auto-player that makes exactly the choices `Run`
    // makes, and prints the two side by side. They should agree to the last decimal, because on
    // the same seed they make the same calls on the same RNG in the same order. When they stop
    // agreeing, this says so in one line and says which figure moved - which is a great deal more
    // use than discovering it during a week of playing.
    //
    // WHAT IT DOES NOT COVER, and cannot: the table reads the Impact die off the felt rather than
    // rolling it, and the felt's die explodes by being picked up and thrown again. That is the
    // same rule reached by a different route, and it is `game/Diagnostics/FightCheck.cs` that
    // holds the felt to it - on real physics dice, which no report here has.
    //
    // `EncounterTests.DrivenLikeRun_ItIsRun` is the same property as a test, on three seeds. This
    // is the same property over twenty thousand fights, which is where a difference of one in a
    // thousand shows up.
    //
    // AND THEN THE SECOND QUESTION, WHICH IS A BALANCE ONE. `Run` has no initiative in it, so the
    // faithfulness comparison runs with `CombatOptions.RollInitiative` off. Turning it on is a
    // real rules change - one throw at the top decides whether the hero swings before the room
    // does - and CONVENTIONS.md 8 says a rules change gets simulated rather than felt. The second
    // table is that measurement, and it is the first time initiative has been measured at all,
    // because until C3 nothing implemented it.
    static class PlayerPathReport
    {
        // the one place this report names a concrete roster - see EncounterReport
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

            // and what the rule Run does not have actually costs
            Table.Title("What initiative costs (CORE_RULES.md section 8)");
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

            // exact, not close. the same seed through the same calls is the same fight, so
            // anything but zero difference is a difference in the rules
            public bool Same(Stats other) =>
                WinPct == other.WinPct && AvgRounds == other.AvgRounds && AvgVigorLeft == other.AvgVigorLeft;
        }

        static void WriteRow(string label, Stats s) =>
            Table.Row(label, Table.Pct(s.WinPct), Table.Num(s.AvgRounds), Table.Num(s.AvgVigorLeft));

        static Stats Sweep(int trials, bool played, bool initiative)
        {
            // the same seed every way round, and one resolver across every trial, exactly as
            // EncounterReport does it - the sequence matters as much as the seed
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

        // the auto-player. It is `Run`'s own choices, made from outside: the hero swings at
        // whatever the engine's targeting picks, every foe swings at the hero, and nothing else
        // happens. That is deliberately a poor player - it never moves, never spends Nerve and
        // never retreats - because the question here is whether the PATH is faithful, not whether
        // somebody clever can do better with it
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

                    // out of actions and holding the turn open for a Nerve he is never going to
                    // spend. `Run` takes exactly its actions and moves on, so this says so out
                    // loud - see Encounter.Done for why the hero's turn waits to be told
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

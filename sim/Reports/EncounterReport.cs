using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Sim
{
    // the standard fight - Barbarian against N Rabble and one Rival
    // the report that settled the action economy, at 1, 2 and 3 hero actions a round
    // and the Rabble sweep, which checks the tier adds pressure rather than length
    static class EncounterReport
    {
        // the one place this report names a concrete roster
        // every balance number below comes out of whatever source is bound here, so pointing
        // it at a campaign's statblocks is this line and nothing else
        static readonly IArchetypeSource Archetypes = new BuiltInArchetypes();

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

        public static void ActionEconomy(int trials)
        {
            Table.Title("Encounter: Barbarian vs 4 Rabble + 1 Rival");
            Table.Header("hero actions/round", "win %", "avg rounds", "vigor left");

            foreach (var actions in new[] { 1, 2, 3 })
                WriteRow(actions.ToString(), Run(trials, actions, rabble: 4));
        }

        public static void RabbleSensitivity(int trials)
        {
            Table.Title("Rabble count sensitivity (hero: 2 actions)");
            Table.Header("rabble", "win %", "avg rounds", "vigor left");

            // difficulty should climb steeply while round count stays flat
            // more pressure, not more grind
            foreach (var n in new[] { 2, 4, 6, 8 })
                WriteRow(n.ToString(), Run(trials, actions: 2, rabble: n));
        }

        static void WriteRow(string label, Stats s) =>
            Table.Row(label, Table.Pct(s.WinPct), Table.Num(s.AvgRounds), Table.Num(s.AvgVigorLeft));

        static Stats Run(int trials, int actions, int rabble)
        {
            var engine = new CombatEngine(
                new StandardResolver(new SeededRng(4242)),
                new CombatOptions { HeroActionsPerRound = actions });

            int wins = 0;
            long rounds = 0;
            long vigor = 0;

            for (int i = 0; i < trials; i++)
            {
                var r = engine.Run(
                    Archetypes.Create(EngineIds.Barbarian),
                    Archetypes.Standard(rabble));

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
    }
}

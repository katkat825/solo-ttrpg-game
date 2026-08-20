using System.Linq;
using Core.Dice;
using Core.Resolution;

namespace Sim
{
    // chance to beat each difficulty, per pool
    // the ladder these figures are checked against was verified by exact enumeration
    // so a drift of more than a few tenths of a percent means the resolver has a bug
    static class DifficultyLadderReport
    {
        static readonly int[] Dcs =
        {
            Difficulty.Easy, Difficulty.Standard, Difficulty.Tricky,
            Difficulty.Hard, Difficulty.Formidable, Difficulty.Legendary
        };

        public static void Run(int trials)
        {
            Table.Title("Difficulty ladder: chance to beat each DC");
            Table.Header("pool", Dcs.Select(d => "DC" + d).ToArray());

            foreach (var sample in SamplePools.All)
            {
                var cells = Dcs
                    .Select(dc => Table.Pct(SuccessRate(sample, dc, trials)))
                    .ToArray();

                Table.Row(sample.DebugName, cells);
            }
        }

        static double SuccessRate(SamplePools.Sample sample, int dc, int trials)
        {
            var resolver = new StandardResolver(new SeededRng(1000 + dc));
            int hits = 0;

            for (int i = 0; i < trials; i++)
                if (resolver.Resolve(sample.Build()).Beats(dc)) hits++;

            return 100.0 * hits / trials;
        }
    }
}

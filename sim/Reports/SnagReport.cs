using Core.Dice;
using Core.Resolution;

namespace Sim
{
    // 33%, not 39.2%: snag is exactly one 1, while the "at-least-one" column is both tiers
    static class SnagReport
    {
        public static void Run(int trials)
        {
            Table.Title("Snag and Trouble rates");
            Table.Header("pool", "snag", "trouble");

            foreach (var sample in SamplePools.All)
            {
                var resolver = new StandardResolver(new SeededRng(7));
                int snags = 0, troubles = 0;

                for (int i = 0; i < trials; i++)
                {
                    var r = resolver.Resolve(sample.Build());
                    if (r.Snag) snags++;
                    if (r.Trouble) troubles++;
                }

                Table.Row(sample.DebugName,
                    Table.Pct(100.0 * snags / trials),
                    Table.Pct(100.0 * troubles / trials));
            }
        }
    }
}

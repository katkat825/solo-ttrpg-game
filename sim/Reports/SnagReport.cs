using Rules.Dice;
using Rules.Resolution;

namespace Sim
{
    // how often the companion gets a cue - Snag, ~33% early
    // against how often something actually goes wrong - Trouble, ~6%
    //
    // 33 and not the 39 quoted around this project for a long time: PoolResult.Snag is EXACTLY
    // one 1, and the 39.2% in CORE_RULES 6 is the at-least-one column, which is both tiers
    // this table has always printed 33.5% - PoolOdds now says why, and a test holds it there
    // bigger dice make both rarer, so the chattiest stretch is the early game
    // which is when a player with no party most needs a voice
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

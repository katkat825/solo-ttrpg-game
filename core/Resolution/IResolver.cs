using Core.Dice;

namespace Core.Resolution
{
    // the one resolution seam - it owns its RNG, so a seed replays identically
    public interface IResolver
    {
        PoolResult Resolve(Pool pool);

        int RollImpact(Die impact, bool explodes = true);
    }
}

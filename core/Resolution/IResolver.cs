using Core.Dice;

namespace Core.Resolution
{
    // the one rule, behind a seam
    // every action resolves through here
    // so it can be wrapped, swapped or faked without touching a caller
    // the resolver owns its RNG, so a seeded one replays a session identically
    public interface IResolver
    {
        PoolResult Resolve(Pool pool);

        int RollImpact(Die impact, bool explodes = true);
    }
}

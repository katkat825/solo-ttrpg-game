namespace Core.Dice
{
    // the only source of randomness in the game
    // nothing in core/ may touch System.Random directly
    // that constraint is what keeps tests deterministic and sim runs reproducible
    public interface IRng
    {
        // inclusive 1..sides
        int Roll(int sides);
    }
}

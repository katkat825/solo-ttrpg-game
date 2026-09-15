namespace Core.Dice
{
    // the only source of randomness in core - nothing here may touch System.Random directly
    public interface IRng
    {
        // inclusive 1..sides
        int Roll(int sides);
    }
}

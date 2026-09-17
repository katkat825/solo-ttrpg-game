namespace Content.World
{
    public sealed class Ending : Happened
    {
        public Ending(bool won)
        {
            Won = won;
        }

        public bool Won { get; }

        public override string ToString() =>
            (Won ? "the fight is won" : "the fight is lost") + Said();
    }
}

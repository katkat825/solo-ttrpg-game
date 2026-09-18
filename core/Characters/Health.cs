namespace Core.Characters
{
    // HOW HURT SOMEBODY LOOKS, AND NOTHING MORE PRECISE THAN THAT.
    //
    // Three bands, and that is the whole vocabulary. The pillar is "watch it get worse, no health
    // bar": a number or a gauge over a foe turns a fight into arithmetic, and the player is meant
    // to be reading the table. A band is also the kinder thing for a screen reader and for a
    // colourblind player, because it is a word rather than a length of red.
    //
    // The thresholds are NOT new numbers. They are the ones Actor.Damage already crosses when it
    // applies Winded and Reeling, so the band and the conditions can never disagree about how the
    // fight is going.
    public enum Health
    {
        Unharmed,

        Wounded,

        Badly,
    }

    public static class Healths
    {
        public static string Word(this Health band) => band switch
        {
            Health.Badly => "badly_wounded",
            _ => band.ToString().ToLowerInvariant(),
        };

        // a Rabble has no health track at all, so it is whole until it is gone
        public static Health Of(Actor actor) =>
            actor == null ? Health.Unharmed
          : !actor.Tier.HasHealthTrack() ? Health.Unharmed
          : Of(actor.Vigor, actor.MaxVigor);

        public static Health Of(int vigor, int maxVigor)
        {
            if (maxVigor <= 0) return Health.Unharmed;

            if (vigor <= maxVigor / 3.0) return Health.Badly;

            if (vigor <= maxVigor * 2 / 3.0) return Health.Wounded;

            return Health.Unharmed;
        }
    }
}

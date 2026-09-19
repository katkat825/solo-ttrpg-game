namespace Game.Fight
{
    // WHAT A NERVE BUYS RIGHT NOW.
    //
    // The cadence was real and worked and existed only as an if-ladder inside the click handler,
    // which meant the one thing it could not do was be shown to anybody. A player pressing a token
    // found out what it did by pressing it, and the sheet - the paper that is supposed to be the
    // canonical record of the resource - printed the count and not one word about what the count
    // was for.
    //
    // So the ladder is here, named, and Godot-free. The token asks it what to do; the sheet asks
    // it what to say; the note the DM pushes when you run out of actions asks it whether pushing
    // is even open. One rule, three readers, and no second copy to drift - the same argument as
    // the locale audit's.
    public enum Spend
    {
        // there is a Nerve, or there is not, and either way nothing is waiting on it
        Nothing,

        // a Trouble is on the felt and has not been shrugged; this is the only one with a deadline
        Shrug,

        // the felt is being read and a die may be picked up and thrown again
        Rethrow,

        // a Heart die is committed to the next throw, and touching a token takes it back out
        TakeItBack,

        // the next pool can take the Heart die: a fourth die, harder as well as likelier
        HeartDie,

        // one more action in a turn that has run out of them
        Push,
    }

    public static class NerveUse
    {
        // Ordered exactly as the token's own handler runs, because it IS the token's own handler.
        // The felt comes first because it is the only part of this with a deadline: a Trouble
        // unshrugged when the window closes is a Trouble that landed.
        public static Spend For(bool reading, bool trouble, bool shrugged, bool awaitingHero,
                                bool committed, int actionsLeft, int nerve, bool canAddHeart)
        {
            if (nerve <= 0 && !committed) return Spend.Nothing;

            if (reading)
            {
                if (trouble && !shrugged) return Spend.Shrug;

                return nerve > 0 ? Spend.Rethrow : Spend.Nothing;
            }

            if (!awaitingHero) return Spend.Nothing;

            // taking it back costs nothing, which is why it is asked before anything that spends
            if (committed) return Spend.TakeItBack;

            if (actionsLeft > 0 && canAddHeart) return Spend.HeartDie;

            return Spend.Push;
        }

        public static string Word(this Spend spend) => spend switch
        {
            Spend.TakeItBack => "take_it_back",
            Spend.HeartDie => "heart_die",
            _ => spend.ToString().ToLowerInvariant(),
        };

        // whether this one costs a Nerve. Taking the Heart die back out is free, and so is being
        // told there is nothing to spend it on
        public static bool Costs(this Spend spend) =>
            spend is Spend.Shrug or Spend.Rethrow or Spend.HeartDie or Spend.Push;
    }
}

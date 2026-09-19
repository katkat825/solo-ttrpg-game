using Core.Localization;
using Game.Fight;

namespace Game.Tests
{
    // WHAT A NERVE BUYS RIGHT NOW (V2).
    //
    // The cadence used to live as an if-ladder inside a click handler, where the one thing it
    // could not do was be shown to anybody - a player learned it by pressing a token and watching
    // what happened, and the sheet printed the count without a word about what the count was for.
    //
    // It is a function now, so it can be asked twice: once by the token, to do the thing, and once
    // by the paper, to say what the thing is. These hold it to that, because the only way the two
    // could ever disagree is if there were two of it.
    public class NerveUseTests
    {
        // the whole table in one call, with the ordinary case as the default
        static Spend For(bool reading = false, bool trouble = false, bool shrugged = false,
                         bool awaitingHero = true, bool committed = false, int actionsLeft = 2,
                         int nerve = 3, bool canAddHeart = true) =>
            NerveUse.For(reading, trouble, shrugged, awaitingHero, committed, actionsLeft, nerve,
                         canAddHeart);

        [Fact]
        public void With_no_nerve_in_hand_there_is_nothing_to_spend()
        {
            Assert.Equal(Spend.Nothing, For(nerve: 0));
            Assert.Equal(Spend.Nothing, For(nerve: 0, actionsLeft: 0));
            Assert.Equal(Spend.Nothing, For(nerve: 0, reading: true, trouble: true));
        }

        [Fact]
        public void The_one_with_a_deadline_is_asked_first()
        {
            // a Trouble unshrugged when the read window closes is a Trouble that landed, and it is
            // the only thing here that stops being available by waiting
            Assert.Equal(Spend.Shrug, For(reading: true, trouble: true));

            // and once it has been shrugged the felt is an ordinary felt again
            Assert.Equal(Spend.Rethrow, For(reading: true, trouble: true, shrugged: true));
        }

        [Fact]
        public void An_open_felt_with_no_trouble_on_it_buys_one_die_thrown_again()
        {
            Assert.Equal(Spend.Rethrow, For(reading: true));
            Assert.Equal(Spend.Nothing, For(reading: true, nerve: 0));
        }

        [Fact]
        public void Taking_the_heart_die_back_out_is_free_and_is_offered_before_anything_that_costs()
        {
            Assert.Equal(Spend.TakeItBack, For(committed: true));

            // free, so it is still the answer with nothing left to spend
            Assert.Equal(Spend.TakeItBack, For(committed: true, nerve: 0));
        }

        [Fact]
        public void A_turn_with_actions_left_buys_a_fourth_die()
        {
            Assert.Equal(Spend.HeartDie, For());

            // a hero whose Heart die is already in the pool, or has none, has nothing to add
            Assert.Equal(Spend.Push, For(canAddHeart: false));
        }

        [Fact]
        public void A_turn_that_has_run_out_of_actions_buys_one_more()
        {
            Assert.Equal(Spend.Push, For(actionsLeft: 0));

            // and it is the fall-through rather than a case of its own: a turn with actions left
            // and no Heart die to add pushes too, which is what the token has always done
            Assert.Equal(Spend.Push, For(actionsLeft: 3, canAddHeart: false));
        }

        [Fact]
        public void Somebody_elses_turn_buys_nothing_off_the_table()
        {
            Assert.Equal(Spend.Nothing, For(awaitingHero: false));
            Assert.Equal(Spend.Nothing, For(awaitingHero: false, actionsLeft: 0));

            // except on an open felt, which is yours whoever is acting
            Assert.Equal(Spend.Shrug, For(awaitingHero: false, reading: true, trouble: true));
        }

        [Fact]
        public void Only_the_ones_that_take_a_chip_off_the_sheet_say_they_cost_one()
        {
            Assert.True(Spend.Shrug.Costs());
            Assert.True(Spend.Rethrow.Costs());
            Assert.True(Spend.HeartDie.Costs());
            Assert.True(Spend.Push.Costs());

            Assert.False(Spend.TakeItBack.Costs());
            Assert.False(Spend.Nothing.Costs());
        }


        // ---- the words ---------------------------------------------------------------------

        [Fact]
        public void Every_spend_has_a_key_and_all_of_them_obey_the_grammar()
        {
            foreach (Spend spend in System.Enum.GetValues<Spend>())
            {
                string key = TurnKeys.Of(spend);

                Assert.StartsWith("ui.nerve.", key);
                Assert.True(KeyConventions.IsWellFormed(key), key);
            }

            Assert.Equal("ui.nerve.heart_die", TurnKeys.Of(Spend.HeartDie));
            Assert.Equal("ui.nerve.take_it_back", TurnKeys.Of(Spend.TakeItBack));
        }

        [Fact]
        public void The_turn_owns_the_marker_and_both_rows_of_the_note()
        {
            Assert.Equal("ui.turn.end_turn", TurnKeys.EndTurn);
            Assert.Equal("ui.turn.push", TurnKeys.Push);
            Assert.Equal("ui.turn.stop", TurnKeys.Stop);

            foreach (string key in TurnKeys.All())
                Assert.True(KeyConventions.IsWellFormed(key), key);
        }

        [Fact]
        public void Adding_a_spend_owes_a_key_because_the_list_is_derived_and_never_listed()
        {
            int spends = System.Enum.GetValues<Spend>().Length;

            Assert.Equal(spends + 3, System.Linq.Enumerable.Count(TurnKeys.All()));
        }

        [Fact]
        public void The_push_row_is_the_only_one_that_counts_a_number_in()
        {
            // "Push on - one more action, for a Nerve. You have {0}." A translation that dropped
            // the placeholder would otherwise read as a deliberate choice rather than as a bug
            Assert.True(TurnKeys.TakesAnArgument(TurnKeys.Push));

            Assert.False(TurnKeys.TakesAnArgument(TurnKeys.Stop));
            Assert.False(TurnKeys.TakesAnArgument(TurnKeys.EndTurn));
            Assert.False(TurnKeys.TakesAnArgument(TurnKeys.Of(Spend.Push)));
        }
    }
}

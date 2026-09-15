using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Game.Companion;

namespace Game.Tests
{
    // W1. The companion itself needs an engine, so what is tested here is everything that decides
    // what it does: which idle comes next, what the table just did, and how long a line is up for.
    // If those are right, the Node3D is a body with a tween on it.
    public class CompanionTests
    {
        // ---- idles ------------------------------------------------------------------------

        [Fact]
        public void Idles_are_dealt_so_the_loop_cannot_be_counted()
        {
            var idling = new Idling(6, new SeededRng(4242), hold: 1.0, jitter: 0.0);

            var seen = new List<int> { idling.Idle };

            for (int i = 0; i < 5; i++)
            {
                idling.Tick(1.0);
                seen.Add(idling.Idle);
            }

            Assert.Equal(6, seen.Distinct().Count());
        }

        [Fact]
        public void An_idle_never_follows_itself()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var idling = new Idling(3, new SeededRng(seed), hold: 1.0, jitter: 0.0);

                int was = idling.Idle;

                for (int i = 0; i < 12; i++)
                {
                    idling.Tick(1.0);

                    Assert.NotEqual(was, idling.Idle);

                    was = idling.Idle;
                }
            }
        }

        [Fact]
        public void Ticking_reports_only_the_frame_the_idle_changed()
        {
            var idling = new Idling(4, new SeededRng(7), hold: 1.0, jitter: 0.0);

            Assert.False(idling.Tick(0.4));
            Assert.False(idling.Tick(0.4));
            Assert.True(idling.Tick(0.4));
        }

        // even spacing is what gives a loop away, so the holds must not all be the same
        [Fact]
        public void The_holds_are_not_evenly_spaced()
        {
            var idling = new Idling(12, new SeededRng(11));

            var holds = new List<double>();

            for (int i = 0; i < 12; i++)
            {
                holds.Add(idling.Left);
                idling.Tick(idling.Left);
            }

            Assert.True(holds.Distinct().Count() > 6, "every idle was held for exactly as long");
            Assert.All(holds, h => Assert.InRange(h, 0.1, Idling.DefaultHold + Idling.DefaultJitter));
        }

        [Fact]
        public void Breaking_an_idle_cuts_it_short_rather_than_waiting_it_out()
        {
            var idling = new Idling(5, new SeededRng(3));

            int was = idling.Changes;

            idling.Break();
            idling.Tick(0.0);

            Assert.Equal(was + 1, idling.Changes);
        }

        [Fact]
        public void A_companion_with_no_idles_at_all_stands_still_rather_than_throwing()
        {
            var idling = new Idling(0, new SeededRng(1));

            Assert.False(idling.Tick(10.0));
            Assert.Equal(0, idling.Idle);
        }


        // ---- what the table just did -------------------------------------------------------

        static PoolResult Roll(params int[] values)
        {
            var pool = new Pool();

            foreach (int _ in values) pool.Add(Attr.Might.Key(), Die.D6);

            return new StandardResolver(new ScriptedRng(values)).Resolve(pool);
        }

        [Fact]
        public void One_1_is_a_snag_and_two_are_a_trouble()
        {
            Assert.Equal(Bark.Snag, TableCues.For(Roll(1, 4, 5)));
            Assert.Equal(Bark.Trouble, TableCues.For(Roll(1, 1, 5)));
            Assert.Null(TableCues.For(Roll(3, 4, 5)));
        }

        [Fact]
        public void A_trouble_is_read_as_trouble_even_though_it_is_also_at_least_one_1()
        {
            // the 39% figure that gets quoted is both tiers added; these are the two tiers
            Assert.Equal(Bark.Trouble, TableCues.For(Roll(1, 1, 1)));
        }

        [Fact]
        public void Nothing_at_all_is_a_quiet_throw_and_not_a_crash()
        {
            Assert.Null(TableCues.For(null));
        }

        [Fact]
        public void A_hero_on_the_floor_goes_quiet_and_nothing_talks_it_out_of_that()
        {
            Assert.Equal(Mood.Quiet, TableCues.MoodFor(Bark.Down));

            Assert.False(TableCues.Outranks(Mood.Pleased, Mood.Quiet));
            Assert.False(TableCues.Outranks(Mood.Alert, Mood.Quiet));
            Assert.False(TableCues.Outranks(Mood.Watching, Mood.Quiet));
        }

        [Fact]
        public void A_sharper_mood_takes_over_from_a_calmer_one()
        {
            Assert.True(TableCues.Outranks(Mood.Alert, Mood.Watching));
            Assert.True(TableCues.Outranks(Mood.Quiet, Mood.Pleased));
            Assert.False(TableCues.Outranks(Mood.Watching, Mood.Alert));
        }

        [Fact]
        public void Every_situation_has_a_mood_so_a_new_bark_cannot_land_nowhere()
        {
            foreach (Bark situation in System.Enum.GetValues<Bark>())
                Assert.True(System.Enum.IsDefined(typeof(Mood), TableCues.MoodFor(situation)));
        }


        // ---- how long a line is up ---------------------------------------------------------

        [Fact]
        public void A_short_line_still_gets_long_enough_to_register()
        {
            Assert.Equal(Reading.Shortest, Reading.Time("Hm."));
        }

        [Fact]
        public void A_long_line_is_capped_rather_than_hanging_there()
        {
            Assert.Equal(Reading.Longest, Reading.Time(new string('x', 4000)));
        }

        [Fact]
        public void A_longer_line_is_up_longer()
        {
            Assert.True(Reading.Time(new string('x', 120)) > Reading.Time(new string('x', 40)));
        }

        [Fact]
        public void Nothing_is_up_for_no_time()
        {
            Assert.Equal(0.0, Reading.Time(""));
            Assert.Equal(0.0, Reading.Time(null));
        }

        // W6's whole promise: adding a voice clip never shortens the text's time on screen
        [Fact]
        public void A_clip_can_only_lengthen_a_lines_time_on_screen_never_shorten_it()
        {
            string line = new string('x', 100);

            double silent = Reading.Time(line);

            Assert.Equal(silent, Reading.Time(line, 0.0));
            Assert.True(Reading.Time(line, 0.5) >= silent);
            Assert.True(Reading.Time(line, 12.0) > silent);
        }
    }
}

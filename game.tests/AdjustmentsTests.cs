using System;
using System.IO;
using System.Linq;
using Godot;
using Game.Access;
using Game.Book;
using Game.Companion;

namespace Game.Tests
{
    // EVERY DIAL THE PLAYER CAN TURN, AND THE PAGE THAT TURNS THEM (AX1, AX3, AX4, AX5).
    //
    // The settings page was printed with its headings and nothing turnable for a whole phase, on the
    // grounds that the dials behind it were the accessibility build. This is that build's half of the
    // bargain: every line on the page cycles, every choice means something measurable, and what the
    // player chose is still true next time they sit down.
    public class AdjustmentsTests
    {
        static string Scratch()
        {
            string folder = Path.Combine(Path.GetTempPath(),
                                         "ax_" + Guid.NewGuid().ToString("N").Substring(0, 8));

            Directory.CreateDirectory(folder);

            return folder;
        }


        // ---- what each one means ----------------------------------------------------------------

        [Fact]
        public void A_new_player_gets_the_room_as_it_was_designed()
        {
            var how = new Adjustments();

            Assert.Equal(1f, how.TextScale);
            Assert.False(how.HighContrast);
            Assert.False(how.Captions);
            Assert.False(how.ReadAloud);
            Assert.Equal(Reading.Pace, how.Speed);
            Assert.Equal(Shown.Both, how.Showing);
        }

        [Fact]
        public void Bigger_letters_are_bigger_and_smaller_ones_are_smaller()
        {
            var how = new Adjustments { Lettering = Lettering.Largest };

            Assert.True(how.TextScale > 1f);

            how.Lettering = Lettering.Small;

            Assert.True(how.TextScale < 1f);
        }

        [Fact]
        public void Slowing_the_speech_leaves_a_line_up_longer_including_a_long_one()
        {
            const string bark = "That was the hand, not the arm, and I would not lean on it again.";

            var normal = new Adjustments();
            var slow = new Adjustments { Speech = Pace.Slow };
            var quick = new Adjustments { Speech = Pace.Quick };

            Assert.True(slow.Time(bark) > normal.Time(bark));
            Assert.True(quick.Time(bark) < normal.Time(bark));

            // AND THE CEILING MOVES WITH IT. At the fixed nine-second ceiling a long line came and
            // went at exactly the same speed however slow the setting was, which is the setting
            // doing nothing to the lines that most need it
            string long_ = string.Join(" ", Enumerable.Repeat(bark, 3));

            Assert.True(slow.Time(long_) > normal.Time(long_));
        }

        [Fact]
        public void Nothing_at_any_speed_is_up_for_less_than_long_enough_to_register()
        {
            foreach (Pace pace in Enum.GetValues<Pace>())
                Assert.True(new Adjustments { Speech = pace }.Time("Hm.") >= Reading.Shortest);
        }

        [Fact]
        public void The_voice_is_paced_with_the_page_rather_than_racing_it()
        {
            Assert.True(new Adjustments { Speech = Pace.Slow }.Rate < 1f);
            Assert.True(new Adjustments { Speech = Pace.Quick }.Rate > 1f);
        }

        [Fact]
        public void A_pace_of_nothing_is_not_a_line_that_never_goes_away()
        {
            Assert.True(Reading.At("Hm.", 0.0) > 0.0);
            Assert.True(Reading.At("Hm.", -5.0) < 1000.0);
        }

        [Fact]
        public void At_the_ordinary_pace_nothing_about_the_old_timings_moved()
        {
            const string line = "The door gives, and something behind it does not.";

            Assert.Equal(Reading.Time(line), Reading.At(line, Reading.Pace));
        }


        // ---- the page ---------------------------------------------------------------------------

        [Fact]
        public void Every_line_on_the_page_has_somewhere_to_go_except_the_one_that_says_it_has_not()
        {
            foreach (Setting setting in Enum.GetValues<Setting>())
                Assert.Equal(setting != Setting.Volume, setting.Built());
        }

        [Fact]
        public void Turning_a_line_goes_round_one_and_comes_back_to_where_it_started()
        {
            var how = new Adjustments();

            foreach (Setting setting in Enum.GetValues<Setting>())
            {
                if (!setting.Built()) continue;

                string was = setting.Value(how);

                for (int turn = 0; turn < setting.Choices().Count; turn++)
                    Assert.True(setting.Turn(how));

                Assert.Equal(was, setting.Value(how));
            }
        }

        [Fact]
        public void A_line_that_does_not_turn_yet_does_not_turn()
        {
            var how = new Adjustments();

            Assert.False(Setting.Volume.Turn(how));
            Assert.Empty(Setting.Volume.Choices());
        }

        [Fact]
        public void Turning_a_line_with_nothing_behind_it_changes_nothing()
        {
            Assert.False(Setting.TextSize.Turn(null));
        }

        [Fact]
        public void Every_line_reads_back_a_value_it_offers()
        {
            var how = new Adjustments();

            foreach (Setting setting in Enum.GetValues<Setting>())
            {
                if (!setting.Built()) continue;

                Assert.Contains(setting.Value(how), setting.Choices());
            }
        }

        [Fact]
        public void A_value_the_line_does_not_offer_is_refused()
        {
            var how = new Adjustments();

            Assert.False(Setting.TextSize.Set(how, "enormous"));
            Assert.False(Setting.Arms.Set(how, ""));
            Assert.True(Setting.Arms.Set(how, "goblin"));
            Assert.Equal(Arm.Goblin, how.Skin);
        }

        [Fact]
        public void A_built_line_counts_its_value_into_its_sentence_and_the_unbuilt_one_does_not()
        {
            foreach (Setting setting in Enum.GetValues<Setting>())
                Assert.Equal(setting.Built(), Settings.TakesAnArgument(setting.NameKey()));

            // and a value's own word never counts anything in
            foreach (Setting setting in Enum.GetValues<Setting>())
                foreach (string value in setting.Choices())
                    Assert.False(Settings.TakesAnArgument(setting.ValueKey(value)));
        }


        // ---- your arms --------------------------------------------------------------------------

        [Fact]
        public void There_are_real_human_tones_and_a_clearly_non_human_way_out()
        {
            Assert.True(Enum.GetValues<Arm>().Count(a => a.IsHuman()) >= 5);
            Assert.Contains(Arm.Goblin, Enum.GetValues<Arm>().Where(a => !a.IsHuman()));
            Assert.Contains(Arm.Bear, Enum.GetValues<Arm>().Where(a => !a.IsHuman()));
        }

        [Fact]
        public void The_human_tones_are_a_range_rather_than_five_names_for_the_same_colour()
        {
            float[] light = Enum.GetValues<Arm>()
                                .Where(a => a.IsHuman())
                                .Select(a => Contrast.Luminance(a.Paint()))
                                .OrderBy(l => l)
                                .ToArray();

            // darkest to lightest, and a real spread rather than five shades of one
            Assert.True(light.Last() > light.First() * 8f);
        }

        [Fact]
        public void Arms_can_be_two_one_or_none()
        {
            Assert.Equal(2, Shown.Both.Count());
            Assert.Equal(1, Shown.Left.Count());
            Assert.Equal(1, Shown.Right.Count());
            Assert.Equal(0, Shown.Hidden.Count());

            Assert.True(Shown.Left.Left());
            Assert.False(Shown.Left.Right());
        }


        // ---- written down -----------------------------------------------------------------------

        [Fact]
        public void What_the_player_chose_is_still_true_next_time_they_sit_down()
        {
            string folder = Scratch();

            var how = new Adjustments
            {
                Lettering = Lettering.Large,
                HighContrast = true,
                Speech = Pace.Slow,
                Captions = true,
                ReadAloud = true,
                Skin = Arm.Umber,
                Showing = Shown.Left,
            };

            how.Keys.Bind(Act.Help, Key.H);

            Assert.True(how.To(folder));

            Adjustments back = Adjustments.From(folder);

            Assert.Equal(Lettering.Large, back.Lettering);
            Assert.True(back.HighContrast);
            Assert.Equal(Pace.Slow, back.Speech);
            Assert.True(back.Captions);
            Assert.True(back.ReadAloud);
            Assert.Equal(Arm.Umber, back.Skin);
            Assert.Equal(Shown.Left, back.Showing);
            Assert.Equal(Key.H, back.Keys.Of(Act.Help).Key);

            Directory.Delete(folder, true);
        }

        [Fact]
        public void A_folder_with_nothing_in_it_gives_the_defaults_rather_than_an_error()
        {
            Adjustments how = Adjustments.From(Path.Combine(Path.GetTempPath(), "ax_no_such_folder"));

            Assert.Equal(Lettering.Normal, how.Lettering);
            Assert.False(how.Captions);
        }

        [Fact]
        public void A_line_from_a_later_build_costs_the_lines_around_it_nothing()
        {
            var how = new Adjustments();

            int took = how.Read(new[]
            {
                "# somebody's comment",
                "lettering=largest",
                "colourblindness=deuteranopia",
                "contrast=maybe",
                "captions=on",
                "",
            });

            Assert.Equal(2, took);
            Assert.Equal(Lettering.Largest, how.Lettering);
            Assert.True(how.Captions);

            // the one it could not read left its dial alone
            Assert.False(how.HighContrast);
        }

        [Fact]
        public void The_keys_share_the_file_and_can_never_be_read_as_a_dial()
        {
            var how = new Adjustments();

            Assert.All(how.Keys.Lines().Select(l => Adjustments.KeyPrefix + l),
                       line => Assert.StartsWith(Adjustments.KeyPrefix, line));

            // an act called 'captions' would otherwise land on the captions dial
            Assert.DoesNotContain(Acts.Words, word => Settings.Words.Contains(word) &&
                                                      word != "read_aloud");
        }
    }
}

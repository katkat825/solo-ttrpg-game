using System;
using System.Linq;
using Godot;
using Game.Access;

namespace Game.Tests
{
    // WHICH KEY DOES WHICH ACT, AND HOW YOU CHANGE ONE (AX1).
    //
    // Rebinding is the feature most able to leave somebody stranded: a key on two acts, or an essential
    // act on no key at all, and a keyboard player is in a room they cannot get out of. Both are refused
    // rather than documented, and these are what hold that.
    public class BindingsTests
    {
        [Fact]
        public void Every_act_starts_on_the_key_a_player_would_guess()
        {
            var keys = new Bindings();

            Assert.Equal(Key.Tab, keys.Of(Act.ReachNext).Key);
            Assert.False(keys.Of(Act.ReachNext).Shift);

            Assert.Equal(Key.Tab, keys.Of(Act.ReachBack).Key);
            Assert.True(keys.Of(Act.ReachBack).Shift);

            Assert.Equal(Key.Enter, keys.Of(Act.Touch).Key);

            // and the dice have been thrown with the space bar since the tray existed
            Assert.Equal(Key.Space, keys.Of(Act.ThrowDice).Key);

            Assert.Equal(0, keys.Changed);
        }

        [Fact]
        public void Every_act_has_a_key_and_no_two_acts_share_one()
        {
            var keys = new Bindings();

            var bound = Enum.GetValues<Act>().Select(keys.Of).ToList();

            Assert.All(bound, one => Assert.True(one.Any));
            Assert.Equal(bound.Count, bound.Distinct().Count());
        }

        [Fact]
        public void Shift_is_part_of_the_binding_rather_than_a_detail()
        {
            // reaching forward and reaching back are the same key and are not the same binding
            Assert.NotEqual(new Bindings.Bound(Key.Tab), new Bindings.Bound(Key.Tab, shift: true));
        }

        [Fact]
        public void A_key_another_act_already_has_is_refused_and_says_which()
        {
            var keys = new Bindings();

            Assert.Equal(Act.ThrowDice, keys.Clash(new Bindings.Bound(Key.Space), Act.Help));

            Assert.False(keys.Bind(Act.Help, Key.Space));

            // and the act that had it still has it
            Assert.Equal(Key.Space, keys.Of(Act.ThrowDice).Key);
            Assert.Equal(Key.F1, keys.Of(Act.Help).Key);
        }

        [Fact]
        public void Rebinding_to_a_free_key_takes()
        {
            var keys = new Bindings();

            Assert.True(keys.Bind(Act.Help, Key.H));
            Assert.Equal(Key.H, keys.Of(Act.Help).Key);
            Assert.Equal(1, keys.Changed);
        }

        [Fact]
        public void An_act_a_keyboard_player_cannot_do_without_cannot_be_left_unbound()
        {
            var keys = new Bindings();

            Assert.False(keys.Bind(Act.Touch, Key.None));
            Assert.False(keys.Bind(Act.ReachNext, Key.None));

            // and one they can is allowed to go
            Assert.True(keys.Bind(Act.WhereAreWe, Key.None));
            Assert.False(keys.Of(Act.WhereAreWe).Any);
        }


        // ---- arm, then press --------------------------------------------------------------------

        [Fact]
        public void A_key_pressed_with_nothing_armed_binds_nothing()
        {
            var keys = new Bindings();

            Assert.Equal(Bindings.Took.Nothing, keys.Pressed(Key.Z));
            Assert.Equal(0, keys.Changed);
        }

        [Fact]
        public void The_next_key_after_arming_is_the_one_it_takes()
        {
            var keys = new Bindings();

            keys.Arm(Act.Help);

            Assert.Equal(Bindings.Took.Bound, keys.Pressed(Key.H, shift: true));

            Assert.Equal(Key.H, keys.Of(Act.Help).Key);
            Assert.True(keys.Of(Act.Help).Shift);

            // and it is no longer waiting, so the next key you press does whatever it does
            Assert.Null(keys.Armed);
        }

        [Fact]
        public void Escape_lets_go_rather_than_binding_itself()
        {
            var keys = new Bindings();

            keys.Arm(Act.Help);

            Assert.Equal(Bindings.Took.LetGo, keys.Pressed(Key.Escape));

            Assert.Null(keys.Armed);
            Assert.Equal(Key.F1, keys.Of(Act.Help).Key);
        }

        [Fact]
        public void A_taken_key_leaves_the_line_waiting_rather_than_silently_failing()
        {
            var keys = new Bindings();

            keys.Arm(Act.Help);

            Assert.Equal(Bindings.Took.Taken, keys.Pressed(Key.Space));

            // still armed: you pressed the wrong key, you did not ask to stop
            Assert.Equal(Act.Help, keys.Armed);
        }


        // ---- written down -----------------------------------------------------------------------

        [Fact]
        public void What_is_written_down_reads_back_the_same()
        {
            var keys = new Bindings();

            keys.Bind(Act.Help, Key.H);
            keys.Bind(Act.WhereAreWe, Key.W, shift: true);

            var read = new Bindings();

            read.Read(keys.Lines().ToArray());

            foreach (Act act in Enum.GetValues<Act>()) Assert.Equal(keys.Of(act), read.Of(act));
        }

        [Fact]
        public void A_line_naming_an_act_this_build_has_never_heard_of_is_skipped()
        {
            var keys = new Bindings();

            keys.Read(new[] { "pickpocket=p", "help=h", "", "not a line at all", "=x" });

            Assert.Equal(Key.H, keys.Of(Act.Help).Key);
            Assert.Equal(1, keys.Changed);
        }

        [Fact]
        public void A_key_this_build_cannot_name_leaves_that_act_on_its_default()
        {
            var keys = new Bindings();

            keys.Read(new[] { "help=wibble" });

            Assert.Equal(Key.F1, keys.Of(Act.Help).Key);
        }

        [Fact]
        public void The_action_names_are_the_ones_the_engine_already_knows()
        {
            // throw_dice has been an Input action since the tray, and renaming it here would leave
            // the space bar bound to nothing
            Assert.Equal("throw_dice", Act.ThrowDice.Word());

            Assert.All(Acts.Words, word => Assert.DoesNotContain(" ", word));
        }
    }
}

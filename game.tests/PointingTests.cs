using System.Collections.Generic;
using Game.Access;

namespace Game.Tests
{
    // THE HAND THAT REACHES WITHOUT A MOUSE (AX1).
    //
    // The two properties below are why this is a class rather than an index in the room: a hand that
    // loses its place every time the room changes is unusable by keyboard while staying perfectly
    // usable by mouse, and that is the bug nobody notices because nobody who can use the mouse can
    // see it. The rest is bookkeeping, and bookkeeping is what tests are for.
    public class PointingTests
    {
        // a thing you can reach, with no engine behind it: a name, whether it is live, and a count of
        // how many times it was pressed
        sealed class Thing
        {
            public Thing(string called, bool live = true)
            {
                Called = called;
                Live = live;
            }

            public string Called { get; }

            public bool Live { get; }

            public int Pressed { get; private set; }

            public bool Lit { get; private set; }

            public Reachable Reach() =>
                new Reachable(Called, () => Pressed++, live: Live, lit: on => Lit = on);
        }

        static IReadOnlyList<Reachable> Ring(params Reachable[] things) => things;


        // ---- walking it ------------------------------------------------------------------------

        [Fact]
        public void Nothing_is_in_hand_until_you_reach_for_something()
        {
            var hand = new Pointing();

            hand.Over(Ring(new Thing("the door").Reach()));

            Assert.False(hand.Holding);
            Assert.Null(hand.On);
        }

        [Fact]
        public void Reaching_walks_the_list_in_order_and_comes_round_again()
        {
            var hand = new Pointing();

            hand.Over(Ring(new Thing("one").Reach(), new Thing("two").Reach()));

            Assert.True(hand.Next());
            Assert.Equal("one", hand.On.Called);

            Assert.True(hand.Next());
            Assert.Equal("two", hand.On.Called);

            // a ring of objects on a table has no last one
            Assert.True(hand.Next());
            Assert.Equal("one", hand.On.Called);
        }

        [Fact]
        public void Reaching_back_from_nothing_finds_the_last_thing()
        {
            var hand = new Pointing();

            hand.Over(Ring(new Thing("one").Reach(), new Thing("two").Reach()));

            Assert.True(hand.Back());
            Assert.Equal("two", hand.On.Called);
        }

        [Fact]
        public void A_line_you_only_read_is_stepped_over()
        {
            var hand = new Pointing();

            hand.Over(Ring(new Thing("a line of the log", live: false).Reach(),
                           new Thing("turn the page").Reach()));

            Assert.Equal(2, hand.Count);
            Assert.Equal(1, hand.Live);

            hand.Next();

            Assert.Equal("turn the page", hand.On.Called);
        }

        [Fact]
        public void A_page_with_nothing_live_on_it_cannot_be_reached_into()
        {
            var hand = new Pointing();

            hand.Over(Ring(new Thing("a line", live: false).Reach(),
                           new Thing("another", live: false).Reach()));

            Assert.False(hand.Next());
            Assert.False(hand.Back());
            Assert.Null(hand.On);
        }


        // ---- keeping its place -----------------------------------------------------------------

        [Fact]
        public void The_hand_stays_on_what_it_was_on_when_the_room_is_gathered_again()
        {
            var hand = new Pointing();

            var door = new Thing("the door");

            hand.Over(Ring(new Thing("the shelf").Reach(), door.Reach()));
            hand.Next();
            hand.Next();

            Assert.Equal("the door", hand.On.Called);

            // the room laid a page out: a new list, the same objects, one more of them
            hand.Over(Ring(new Thing("a line").Reach(), new Thing("the shelf").Reach(),
                           door.Reach()));

            Assert.Equal("the door", hand.On.Called);
        }

        [Fact]
        public void A_hand_on_something_that_has_gone_lets_go_rather_than_landing_somewhere_else()
        {
            var hand = new Pointing();

            hand.Over(Ring(new Thing("a line of the page").Reach(), new Thing("the shelf").Reach()));
            hand.Next();

            Assert.Equal("a line of the page", hand.On.Called);

            // the book shut
            hand.Over(Ring(new Thing("the shelf").Reach()));

            Assert.False(hand.Holding);
        }


        // ---- pressing, lighting, and the mouse -------------------------------------------------

        [Fact]
        public void Pressing_touches_the_thing_in_hand_and_nothing_else()
        {
            var one = new Thing("one");
            var two = new Thing("two");

            var hand = new Pointing();

            hand.Over(Ring(one.Reach(), two.Reach()));
            hand.Next();

            Assert.True(hand.Press());
            Assert.Equal(1, one.Pressed);
            Assert.Equal(0, two.Pressed);
        }

        [Fact]
        public void Pressing_with_nothing_in_hand_does_nothing()
        {
            var one = new Thing("one");

            var hand = new Pointing();

            hand.Over(Ring(one.Reach()));

            Assert.False(hand.Press());
            Assert.Equal(0, one.Pressed);
        }

        [Fact]
        public void A_thing_you_only_read_cannot_be_pressed_even_if_the_hand_is_put_on_it()
        {
            var line = new Thing("a line", live: false);

            var hand = new Pointing();

            hand.Over(Ring(line.Reach(), new Thing("the page").Reach()));

            // the only way onto it is a mouse, and a mouse cannot press it either
            Assert.False(hand.Press());
            Assert.Equal(0, line.Pressed);
        }

        [Fact]
        public void An_empty_room_holds_nothing_and_says_so()
        {
            var hand = new Pointing();

            hand.Over(null);

            Assert.Equal(0, hand.Count);
            Assert.False(hand.Next());
            Assert.False(hand.Press());
        }
    }
}

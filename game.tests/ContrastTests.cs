using System;
using System.Linq;
using Godot;
using Game.Access;

namespace Game.Tests
{
    // HOW HARD A THING IS TO READ, AS A NUMBER, AND WHETHER ANY COLOUR IS CARRYING MEANING ALONE
    // (AX3).
    //
    // Two halves of the same milestone. The first is arithmetic out of WCAG 2 and is worth testing
    // because a contrast mode built by eye is high-contrast in the two places somebody looked. The
    // second is the register: nothing can look at a ring round a die and decide whether a colourblind
    // player can read it, but it can insist that somebody answered the question.
    public class ContrastTests
    {
        [Fact]
        public void Black_on_white_is_the_most_there_is()
        {
            Assert.Equal(Contrast.Most, Contrast.Between(Colors.Black, Colors.White), 2);
        }

        [Fact]
        public void A_thing_against_itself_is_invisible()
        {
            Assert.Equal(1f, Contrast.Between(Colors.White, Colors.White), 4);
            Assert.Equal(1f, Contrast.Between(new Color("#77462a"), new Color("#77462a")), 4);
        }

        [Fact]
        public void Which_way_round_you_ask_makes_no_difference()
        {
            var ink = new Color("#241f18");
            var paper = new Color("#ddd2b9");

            Assert.Equal(Contrast.Between(ink, paper), Contrast.Between(paper, ink), 5);
        }

        [Fact]
        public void The_books_own_ink_on_its_own_paper_is_readable()
        {
            // the open book: #241f18 on #ddd2b9, tuned by eye long before anybody measured it
            float ratio = Contrast.Between(new Color("#241f18"), new Color("#ddd2b9"));

            Assert.True(ratio >= Contrast.Body, $"the book's own page measures {ratio:0.0}:1");
        }

        [Fact]
        public void High_contrast_picks_the_end_the_paper_is_furthest_from()
        {
            Assert.Equal(Colors.Black, Contrast.Ink(new Color("#ddd2b9")));
            Assert.Equal(Colors.White, Contrast.Ink(new Color("#141210")));

            // and the edge that goes behind the letters is the opposite of the letters
            Assert.Equal(Colors.White, Contrast.Edge(Colors.Black));
            Assert.Equal(Colors.Black, Contrast.Edge(Colors.White));
        }

        [Fact]
        public void The_ink_high_contrast_picks_is_always_enough()
        {
            var papers = new[] { "#ddd2b9", "#141210", "#54452f", "#b7c9e0", "#77462a", "#808080" };

            foreach (string paper in papers)
            {
                var behind = new Color(paper);

                Assert.True(Contrast.Enough(Contrast.Ink(behind), behind, Contrast.Large),
                            $"{paper} carries {Contrast.Between(Contrast.Ink(behind), behind):0.0}:1");
            }
        }


        // ---- nothing means anything by colour alone --------------------------------------------

        [Fact]
        public void Every_cue_the_game_draws_has_a_twin_that_is_not_a_colour()
        {
            foreach (Cue cue in Enum.GetValues<Cue>())
                Assert.True(cue.Carried(),
                            $"{cue.Word()} is carried by colour alone: {cue.How()}");

            Assert.Empty(Redundancies.Bare);
        }

        [Fact]
        public void And_each_one_says_how_rather_than_only_that_it_does()
        {
            foreach (Cue cue in Enum.GetValues<Cue>())
            {
                Assert.False(string.IsNullOrWhiteSpace(cue.How()));

                // the fallback line is the one that says nothing carries it
                Assert.DoesNotContain("which is a bug", cue.How());
            }
        }

        [Fact]
        public void The_register_covers_the_dice_the_conditions_the_health_and_the_map()
        {
            // the four that would matter most to a colourblind player at this table
            Assert.Equal(Twin.Label, Cue.DieCounted.TwinOf());
            Assert.Equal(Twin.Label, Cue.Condition.TwinOf());
            Assert.Equal(Twin.Label, Cue.FoeHealth.TwinOf());
            Assert.Equal(Twin.Shape, Cue.MapTile.TwinOf());
        }

        [Fact]
        public void Each_cue_has_a_word_of_its_own()
        {
            Assert.Equal(Enum.GetValues<Cue>().Length, Redundancies.Words.Distinct().Count());

            Assert.All(Redundancies.Words, word => Assert.DoesNotContain(" ", word));
        }
    }
}

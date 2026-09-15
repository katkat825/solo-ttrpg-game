using System.Linq;
using Content.Dialogue;
using Core.Characters;
using Core.Combat;
using Core.Dice;

namespace Content.Tests
{
    // W3. Camp reads the day, and the test of that is that a night after a bad fight does not open
    // with the scene written for a quiet one.
    public class CampTests
    {
        static Actor Hero() =>
            new Actor("hero", 12, 9)
                .With(Attr.Might, Die.D8)
                .With(Attr.Heart, Die.D6);

        static Actor Foe(Tier tier = Tier.Rival) => new Actor("foe", 8, 9, tier);


        [Fact]
        public void A_day_with_nothing_in_it_is_quiet()
        {
            Assert.Equal(Topic.Quiet, new Day(Hero()).About());
        }

        [Fact]
        public void A_day_that_felled_a_dread_is_about_the_boss()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            day.ActorDowned(Foe(Tier.Dread));
            day.EncounterEnded(new EncounterResult(true, 6, hero.Vigor));

            Assert.True(day.KilledABoss);
            Assert.Equal(Topic.Boss, day.About());
        }

        [Fact]
        public void A_day_the_hero_went_down_outranks_everything_else()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            day.ActorDowned(Foe(Tier.Dread));
            day.ActorDowned(hero);

            Assert.Equal(Topic.Loss, day.About());
        }

        [Fact]
        public void A_day_that_took_the_hero_below_half_is_bloodied()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            hero.Damage(7);
            day.EncounterEnded(new EncounterResult(true, 4, hero.Vigor));

            Assert.True(day.Bloodied);
            Assert.Equal(Topic.Bloodied, day.About());
        }

        [Fact]
        public void A_day_that_spent_nerve_is_about_being_pushed()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            day.NerveChanged(hero, 3, 2);
            day.EncounterEnded(new EncounterResult(true, 3, hero.Vigor));

            Assert.Equal(1, day.NerveSpent);
            Assert.Equal(Topic.Trouble, day.About());
        }

        [Fact]
        public void A_fight_that_cost_nothing_at_all_is_untouched()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            day.EncounterEnded(new EncounterResult(true, 2, hero.Vigor));

            Assert.Equal(Topic.Untouched, day.About());
        }

        [Fact]
        public void Sleeping_clears_the_day_but_not_the_hero()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            day.ActorDowned(Foe(Tier.Dread));
            day.EncounterEnded(new EncounterResult(true, 5, hero.Vigor));

            day.Slept();

            Assert.Equal(0, day.Fights);
            Assert.False(day.KilledABoss);
            Assert.Equal(Topic.Quiet, day.About());
        }

        // a hero still Winded in the morning is worth talking about, and the day did not carry that
        // fact - the hero did. Slept clears the day's record and reads the hero fresh.
        [Fact]
        public void A_hero_who_woke_up_still_hurt_is_still_wounded()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            day.EncounterEnded(new EncounterResult(true, 5, hero.Vigor));
            day.Slept();

            hero.ApplyCondition(Condition.Winded);

            Assert.Equal(Topic.Wounded, day.About());
        }


        const string Scenes = @"
title: after_a_bad_one
speaker: wolf
topic: bloodied
---
You are still bleeding. #line:still_bleeding
===
title: a_second_bad_one
speaker: wolf
topic: bloodied
---
Sit down. #line:sit_down
===
title: nothing_much
speaker: wolf
topic: quiet
---
Quiet, then. #line:quiet_then
===
";

        static Campfire Fire(int seed = 4242) =>
            new Campfire(DialogueBook.Of("greyhollow", ("dialogue/camp.yarn", Scenes)),
                         new SeededRng(seed));

        [Fact]
        public void The_fire_gets_the_scene_the_day_earned()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            hero.Damage(7);
            day.EncounterEnded(new EncounterResult(true, 4, hero.Vigor));

            string tonight = Fire().Tonight(day);

            Assert.Contains(tonight, new[] { "after_a_bad_one", "a_second_bad_one" });
        }

        [Fact]
        public void Two_bad_nights_running_do_not_get_the_same_scene()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            hero.Damage(7);
            day.EncounterEnded(new EncounterResult(true, 4, hero.Vigor));

            Campfire fire = Fire();

            Assert.NotEqual(fire.Tonight(day), fire.Tonight(day));
        }

        // the ONE safe fallback: quiet claims nothing about the day, so it can be said on any night
        [Fact]
        public void A_day_with_no_scene_of_its_own_falls_to_quiet_and_never_to_a_topic_it_did_not_earn()
        {
            Actor hero = Hero();
            var day = new Day(hero);

            day.ActorDowned(Foe(Tier.Dread));
            day.EncounterEnded(new EncounterResult(true, 6, hero.Vigor));

            Assert.Equal(Topic.Boss, day.About());
            Assert.Equal(new[] { Topic.Boss, Topic.Quiet }, day.Offers());

            // the campaign has two 'bloodied' scenes and neither may be reached on an unbloodied night
            Assert.Equal("nothing_much", Fire().Tonight(day));
        }

        [Fact]
        public void A_night_whose_only_honest_topic_is_unwritten_says_nothing_rather_than_the_wrong_thing()
        {
            const string bloodiedOnly = @"
title: after_a_bad_one
speaker: wolf
topic: bloodied
---
You are still bleeding. #line:still_bleeding
===
";

            var onlyBloodied = new Campfire(
                DialogueBook.Of("greyhollow", ("dialogue/camp.yarn", bloodiedOnly)),
                new SeededRng(1));

            Actor hero = Hero();
            var day = new Day(hero);

            day.EncounterEnded(new EncounterResult(true, 2, hero.Vigor));

            Assert.Equal(Topic.Untouched, day.About());
            Assert.Null(onlyBloodied.Tonight(day));
        }

        [Fact]
        public void A_campaign_with_no_camp_scenes_gets_no_scene_rather_than_a_crash()
        {
            var fire = new Campfire(
                DialogueBook.Of("greyhollow",
                                ("dialogue/talk.yarn",
                                 "title: x\nspeaker: wolf\n---\nHello. #line:hello\n===\n")),
                new SeededRng(1));

            Assert.Null(fire.Tonight(new Day(Hero())));
            Assert.Empty(fire.Knows);
        }
    }
}

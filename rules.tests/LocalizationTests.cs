using System;
using System.Collections.Generic;
using System.Linq;
using Rules.Characters;
using Rules.Dice;
using Rules.Localization;
using Rules.Resolution;
using Xunit;

namespace Rules.Tests
{
    // guards that rules/ emits keys and never player-visible text
    // and that every key obeys the one grammar
    // a failure means English has leaked into the engine, or the scheme has started to drift
    // drift is expensive - keys are stable identifiers and renaming one breaks every locale file
    public class LocalizationTests
    {
        // every key the engine can currently produce
        // plus a few campaign-shaped ones it never emits but the grammar still has to accept
        // the engine half comes from EngineKeys rather than being listed again here
        // check-locale.ps1 holds the locale file to that same list
        // two copies would be free to disagree, this one passing while coverage quietly lapsed
        static IEnumerable<string> AllEmittedKeys()
        {
            var hero = BuiltInArchetypes.Barbarian();

            yield return hero.NameKey;
            yield return hero.WeaponKey;
            yield return BuiltInArchetypes.Rabble(3).NameKey;

            foreach (var d in hero.BuildPool(Attr.Might, Skill.Blades).Dice)
                yield return d.LabelKey;

            foreach (string key in EngineKeys.All())
                yield return key;

            // not engine output - these ship with a campaign, or do not exist yet
            // here because the grammar has to hold for them too
            yield return KeyConventions.Bark("wolf", "snag", 17);
            yield return KeyConventions.Line("dm", "narration", "chapter_01", 4);
            yield return KeyConventions.Key(KeyConventions.UiNs, "character_sheet", "title");
            yield return KeyConventions.Key(KeyConventions.CombatNs, "log", "hit");
        }

        // ---- the grammar ----

        [Fact]
        public void EveryKeyTheEngineEmits_ObeysTheGrammar()
        {
            foreach (string key in AllEmittedKeys())
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));
        }

        [Theory]
        [InlineData("actor.barbarian.name")]
        [InlineData("attr.might.name")]
        [InlineData("skill.blades.description")]
        [InlineData("condition.winded.name")]
        [InlineData("gear.axe.name")]
        [InlineData("dialogue.wolf.bark.snag.017")]
        [InlineData("dialogue.dm.narration.chapter_01.017")]
        [InlineData("quest.ashfall.chapter_02.title")]
        [InlineData("ui.character_sheet.title")]
        public void WellFormedKeys_AreAccepted(string key) =>
            Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));

        [Theory]
        [InlineData("actor.Barbarian.name", "uppercase")]
        [InlineData("wolf.bark.snag.017", "speaker at the top level is not a namespace")]
        [InlineData("actor.barbarian", "only two segments")]
        [InlineData("actor barbarian name", "spaces")]
        [InlineData("actor.barbarian-name.x", "hyphen")]
        [InlineData("dialogue.wolf.017.bark", "index not last")]
        [InlineData("", "empty")]
        public void MalformedKeys_AreRejected(string key, string why) =>
            Assert.False(KeyConventions.IsWellFormed(key), $"should have been rejected ({why}): '{key}'");

        [Fact]
        public void Explain_SaysWhatIsWrong()
        {
            Assert.Contains("not a known namespace", KeyConventions.Explain("wolf.bark.snag.017"));
            Assert.Contains("at least 3", KeyConventions.Explain("actor.barbarian"));
            Assert.Contains("not lowercase", KeyConventions.Explain("Actor.barbarian.name"));
            Assert.Contains("indices go last", KeyConventions.Explain("dialogue.wolf.017.bark"));
        }

        // ---- shape of specific keys ----

        [Fact]
        public void TraitKeys_FollowTheConvention()
        {
            Assert.Equal("attr.might.name", Attr.Might.Key());
            Assert.Equal("skill.blades.name", Skill.Blades.Key());
            Assert.Equal("condition.winded.name", Condition.Winded.Key());
            Assert.Equal("condition.winded.description", Condition.Winded.DescriptionKey());
        }

        [Fact]
        public void ActorName_IsAKey_NotText()
        {
            var hero = BuiltInArchetypes.Barbarian();

            Assert.Equal("actor.barbarian.name", hero.NameKey);
            Assert.Equal("gear.axe.name", hero.WeaponKey);
        }

        [Fact]
        public void NumberedActors_UseAFormatKey_NotConcatenation()
        {
            // "Rabble 3" must come from one key with {0}
            // never from gluing a number onto a translated word - word order differs by language
            var mook = BuiltInArchetypes.Rabble(3);

            Assert.Equal("actor.rabble.name_numbered", mook.NameKey);
            Assert.Equal(3, mook.Ordinal);
        }

        [Fact]
        public void PoolDice_CarryKeys_SoTheTrayCanLabelThemInAnyLanguage()
        {
            var keys = BuiltInArchetypes.Barbarian()
                .BuildPool(Attr.Might, Skill.Blades).Dice
                .Select(d => d.LabelKey).ToList();

            Assert.Contains("attr.might.name", keys);
            Assert.Contains("skill.blades.name", keys);
            Assert.Contains("gear.axe.name", keys);
        }

        [Fact]
        public void SpokenLines_GroupBySpeaker_UnderOneNamespace()
        {
            // dialogue.wolf.* stays one contiguous block
            // so a translator can still do one voice in one pass
            // without speakers occupying the top level
            Assert.Equal("dialogue.wolf.bark.snag.017", KeyConventions.Bark("wolf", "snag", 17));
            Assert.Equal("dialogue.imp.bark.trouble.003", KeyConventions.Bark("imp", "trouble", 3));
            Assert.StartsWith("dialogue.wolf.", KeyConventions.Bark("wolf", "camp", 1));
        }

        [Fact]
        public void Indices_AreZeroPadded_SoTheyNeverNeedRenumbering()
        {
            Assert.Equal("dialogue.wolf.bark.snag.001", KeyConventions.Bark("wolf", "snag", 1));
            Assert.Equal("dialogue.wolf.bark.snag.100", KeyConventions.Bark("wolf", "snag", 100));
        }

        // ---- the localizers ----

        [Fact]
        public void DictionaryLocalizer_ResolvesAndFallsBack()
        {
            var loc = new DictionaryLocalizer(new Dictionary<string, string>
            {
                ["actor.barbarian.name"] = "Barbarian",
                ["actor.rabble.name_numbered"] = "Bandit {0}",
            });

            Assert.Equal("Barbarian", loc.Get("actor.barbarian.name"));
            Assert.Equal("Bandit 3", loc.Format("actor.rabble.name_numbered", 3));

            // missing keys echo, so gaps are loud rather than silent
            Assert.Equal("gear.axe.name", loc.Get("gear.axe.name"));
            Assert.False(loc.Has("gear.axe.name"));
        }

        [Fact]
        public void KeyEcho_IsTheDefault_SoMissingTextIsObvious()
        {
            Assert.Equal("ui.anything.title", KeyEchoLocalizer.Instance.Get("ui.anything.title"));
        }

        // ---- the locale checklist ----

        [Fact]
        public void EngineKeys_CoverEveryTraitAndCondition_SoAddingOneAddsItToTheChecklist()
        {
            var keys = new HashSet<string>(EngineKeys.All());

            // derived from the enums, so a new skill lands here with nobody remembering to add it
            // check-locale.ps1 then fails until it has English
            foreach (Skill s in Enum.GetValues<Skill>())
                Assert.Contains(s.Key(), keys);

            foreach (Condition c in Enum.GetValues<Condition>())
                Assert.Contains(c.DescriptionKey(), keys);

            Assert.Contains(Attr.Heart.DescriptionKey(), keys);
        }

        [Fact]
        public void EngineKeys_FollowTheRoster_NotAHardCodedList()
        {
            var keys = new HashSet<string>(EngineKeys.All());

            foreach (string id in new BuiltInArchetypes().Ids)
            {
                Assert.Contains(KeyConventions.ActorName(id), keys);
                Assert.Contains(KeyConventions.ActorNameNumbered(id), keys);
            }

            // gear comes off the archetypes that carry it, not from a list beside them
            Assert.Contains("gear.axe.name", keys);
            Assert.Contains("gear.club.name", keys);

            // Actor's own default, for anyone who reaches a fight empty-handed
            Assert.Contains("gear.unarmed.name", keys);
        }

        [Fact]
        public void EngineKeys_StopAtTheEngine_SoCampaignsCarryTheirOwnText()
        {
            // a campaign ships its own strings
            // if dialogue ever leaked into the engine checklist
            // game/locale/ would be asked to cover every bark in every campaign ever written
            foreach (string key in EngineKeys.All())
                Assert.Contains(key.Split('.')[0], EngineKeys.Namespaces);

            Assert.DoesNotContain(KeyConventions.DialogueNs, EngineKeys.Namespaces);
            Assert.DoesNotContain(KeyConventions.QuestNs, EngineKeys.Namespaces);
            Assert.DoesNotContain(KeyConventions.CampaignNs, EngineKeys.Namespaces);
        }

        [Fact]
        public void EngineKeys_AreUnique_SoALocaleFileCanKeyOnThem()
        {
            var all = EngineKeys.All().ToList();
            Assert.Equal(all.Count, all.Distinct().Count());
        }

        [Fact]
        public void KeysTakingANumber_AreIdentifiable_SoALocaleCanBeCheckedForPlaceholders()
        {
            Assert.True(EngineKeys.TakesAnArgument(KeyConventions.ActorNameNumbered("rabble")));
            Assert.False(EngineKeys.TakesAnArgument(KeyConventions.ActorName("rabble")));
            Assert.False(EngineKeys.TakesAnArgument(Attr.Might.Key()));
        }
    }
}

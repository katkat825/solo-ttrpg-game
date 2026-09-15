using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Classes;
using Content.Items;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Xunit;

namespace Content.Tests
{
    public sealed class ClassTests : IDisposable
    {
        readonly string _root;

        public ClassTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "classes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        const string Pack = "hearthguard";

        const string Warden = @"{
            ""id"": ""warden"",
            ""vigor"": 20,
            ""defense"": 11,
            ""attributes"": { ""might"": ""d8"", ""grace"": ""d6"", ""wits"": ""d6"", ""heart"": ""d6"" },
            ""skills"": { ""blades"": ""d6"", ""brawl"": ""d6"" },
            ""wields"": ""warden_glaive"",
            ""kit"": [ ""steady_guard"" ],
            ""mini"": ""barbarian""
        }";

        static Read<ClassCard> Parse(string json, string pack = Pack) =>
            ClassReader.Parse(json, "warden.json", pack);

        static ClassCard Good(string json, string pack = Pack)
        {
            Read<ClassCard> read = Parse(json, pack);

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            return read.Value;
        }

        static string Why(string json, string pack = Pack)
        {
            Read<ClassCard> read = Parse(json, pack);

            Assert.False(read.Ok, "this was supposed to be refused");

            return string.Join("; ", read.Problems.Select(p => p.What));
        }


        [Fact]
        public void TheIdIsScopedByThePackThatWroteIt()
        {
            Assert.Equal("hearthguard.warden", Good(Warden).Id);
            Assert.Equal("other.warden", Good(Warden, "other").Id);
        }

        [Fact]
        public void AnUnscopedPackLeavesTheIdAlone()
        {
            Assert.Equal("warden", Good(Warden, "").Id);
        }

        [Fact]
        public void ItNamesItselfInTheClassNamespaceAndTheActorOne()
        {
            ClassCard card = Good(Warden);

            Assert.Equal("class.hearthguard.warden.name", card.NameKey);
            Assert.Equal("class.hearthguard.warden.description", card.DescriptionKey);

            Assert.Contains("actor.hearthguard.warden.name", card.Keys());

            foreach (string key in card.Keys())
                Assert.Equal(KeyConventions.WellFormed, KeyConventions.Explain(key));
        }

        [Fact]
        public void ClassIsAKnownNamespace()
        {
            Assert.Contains(KeyConventions.ClassNs, KeyConventions.Namespaces);
        }

        [Fact]
        public void ADottedIdIsRefusedBecauseThePackAddsItsOwnName()
        {
            Assert.Contains("the pack's name is added for you",
                            Why(Warden.Replace(@"""warden""", @"""hearthguard.warden""")));
        }


        [Theory]
        [InlineData("tier", @"""tier"": ""dread""", "a class is always the hero")]
        [InlineData("behaviour", @"""behaviour"": ""strongest_first""", "picked for by the player")]
        [InlineData("loot", @"""loot"": []", "nobody loots the hero")]
        public void TheFieldsAMonsterHasAndAHeroHasNotAreRefusedWithTheReason(
            string field, string json, string because)
        {
            string why = Why(Warden.Replace(@"""vigor"": 20,", json + @", ""vigor"": 20,"));

            Assert.Contains($"a class has no '{field}'", why);
            Assert.Contains(because, why);
        }

        [Fact]
        public void ANameInTheFileIsRefusedWithTheCsvLineItShouldHaveBeen()
        {
            string why = Why(Warden.Replace(@"""vigor"": 20,", @"""name"": ""Warden"", ""vigor"": 20,"));

            Assert.Contains("it is a key, so it can be translated", why);
            Assert.Contains("class.warden.name", why);
        }

        [Fact]
        public void ADescriptionInTheFileIsRefusedTheSameWay()
        {
            Assert.Contains("class.warden.description",
                            Why(Warden.Replace(@"""vigor"": 20,", @"""description"": ""x"", ""vigor"": 20,")));
        }

        [Fact]
        public void AnUnknownFieldIsATypoAndIsNamedWithWhatTheFieldsAre()
        {
            string why = Why(Warden.Replace(@"""vigor"": 20,", @"""vigour"": 20, ""vigor"": 20,"));

            Assert.Contains("a class has no 'vigour'", why);
            Assert.Contains("wields", why);
        }


        [Fact]
        public void EveryProblemComesBackAtOnce()
        {
            Read<ClassCard> read = Parse(@"{
                ""id"": ""warden"",
                ""vigor"": 0,
                ""attributes"": { ""mite"": ""d8"", ""grace"": ""d7"" }
            }");

            Assert.False(read.Ok);

            // four seeded mistakes: no defense, vigor below one, bad attribute, bad die
            Assert.True(read.Problems.Count >= 4,
                        string.Join("; ", read.Problems.Select(p => p.What)));
        }

        [Fact]
        public void AClassWithNoAttributesIsRefusedBecauseTheHeroIsWhatThrowsThePool()
        {
            Assert.Contains("a hero with none throws no pool at all",
                            Why(@"{ ""id"": ""warden"", ""vigor"": 20, ""defense"": 11 }"));
        }

        [Fact]
        public void SomethingThatIsNotJsonSaysSoWithTheLine()
        {
            Read<ClassCard> read = Parse(@"{ ""id"": ""warden"", }}");

            Assert.False(read.Ok);
            Assert.Contains("this is not JSON", read.Problems[0].What);
        }


        [Fact]
        public void TheKitIsCarried()
        {
            Assert.Equal(new[] { "steady_guard" }, Good(Warden).Kit);
        }

        [Fact]
        public void AMalformedAbilityIdIsStillRefused()
        {
            Assert.Contains("is not an ability id",
                            Why(Warden.Replace(@"[ ""steady_guard"" ]", @"[ ""Steady Guard"" ]")));
        }

        [Fact]
        public void AnAbilityListedTwiceIsRefused()
        {
            Assert.Contains("is in this kit twice",
                            Why(Warden.Replace(@"[ ""steady_guard"" ]",
                                               @"[ ""steady_guard"", ""steady_guard"" ]")));
        }


        [Fact]
        public void TheGearDieComesFromTheItemAndNotFromTheClass()
        {
            ItemCatalogue items = ItemCatalogue.Of(new[] { new Gear("warden_glaive", Die.D10) });

            Assert.Equal(Die.D10, Good(Warden).Create(items).Weapon);
        }

        [Fact]
        public void AClassCanStartYouWithGearTheEngineShips()
        {
            Assert.True(SharedGear.Has("axe"));
            Assert.Equal(new BuiltInArchetypes().Create(EngineIds.Barbarian).Weapon,
                         SharedGear.Of("axe").Die);
        }

        [Fact]
        public void GearThatIsNotThereLeavesTheHandsEmptyRatherThanThrowing()
        {
            Actor hero = Good(Warden).Create(ItemCatalogue.Of(Array.Empty<Gear>()));

            Assert.Equal(Die.None, hero.Weapon);
        }

        [Fact]
        public void ArmourRaisesDefenseThroughTheGearItNames()
        {
            ClassCard card = Good(Warden.Replace(@"""kit"":", @"""wears"": ""plate"", ""kit"":"));

            ItemCatalogue items = ItemCatalogue.Of(new[]
            {
                new Gear("warden_glaive", Die.D6),
                new Gear("plate", Die.None, Skill.None, defense: 2),
            });

            // 11 base + 2 from plate
            Assert.Equal(13, card.Create(items).Defense);
        }


        [Fact]
        public void ANerveCapIsOptionalAndDefaultsToWhatEveryHeroHasHad()
        {
            Assert.Equal(Core.Combat.Nerve.Cap, Good(Warden).Create().NerveCap);
        }

        [Fact]
        public void AClassThatLowersTheCapDoesNotStartOverIt()
        {
            Actor hero = Good(Warden.Replace(@"""vigor"": 20,", @"""nerve"": 2, ""vigor"": 20,")).Create();

            Assert.Equal(2, hero.NerveCap);
            Assert.Equal(2, hero.Nerve);
        }

        [Fact]
        public void ACapOfNoneIsRefusedBecauseItSwitchesOffARule()
        {
            Assert.Contains("which is the Nerve system switched off",
                            Why(Warden.Replace(@"""vigor"": 20,", @"""nerve"": 0, ""vigor"": 20,")));
        }


        [Fact]
        public void AFolderOfClassesIsAnArchetypeSource()
        {
            Write("classes/warden.json", Warden);

            ClassRoster roster = ClassRoster.Read(Path.Combine(_root, "classes"), Pack);

            Assert.Empty(roster.Problems);
            Assert.True(roster.Has("hearthguard.warden"));

            IArchetypeSource source = roster;

            Assert.Equal("hearthguard.warden", source.Create("hearthguard.warden").Id);
        }

        [Fact]
        public void EveryHeroIsANewOne()
        {
            ClassRoster roster = ClassRoster.Of(Pack, Good(Warden));

            Actor first = roster.Create("hearthguard.warden");
            first.Damage(5);

            Assert.Equal(20, roster.Create("hearthguard.warden").Vigor);
        }

        [Fact]
        public void OneBrokenClassDoesNotTakeTheFolderDown()
        {
            Write("classes/warden.json", Warden);
            Write("classes/broken.json", @"{ ""id"": ""broken"" }");

            ClassRoster roster = ClassRoster.Read(Path.Combine(_root, "classes"), Pack);

            Assert.True(roster.Has("hearthguard.warden"));
            Assert.NotEmpty(roster.Problems);
            Assert.Contains(roster.Problems, p => p.File == "broken.json");
        }

        [Fact]
        public void AMissingFolderIsNotAProblem()
        {
            ClassRoster roster = ClassRoster.Read(Path.Combine(_root, "nothing-here"), Pack);

            Assert.Empty(roster.Ids);
            Assert.Empty(roster.Problems);
        }

        [Fact]
        public void TwoClassesWithOneIdAreNamed()
        {
            Write("classes/warden.json", Warden);
            Write("classes/twin.json", Warden);

            ClassRoster roster = ClassRoster.Read(Path.Combine(_root, "classes"), Pack);

            Assert.Contains(roster.Problems, p => p.What.Contains("already the id of another class"));
        }


        [Fact]
        public void TheWardenIsTheBarbarian()
        {
            ClassCard card = Good(Warden);

            ItemCatalogue items = ItemCatalogue.Of(new[] { new Gear("warden_glaive", Die.D6) });

            Actor warden = card.Create(items);
            Actor barbarian = new BuiltInArchetypes().Create(EngineIds.Barbarian);

            Assert.Equal(barbarian.MaxVigor, warden.MaxVigor);
            Assert.Equal(barbarian.Defense, warden.Defense);
            Assert.Equal(barbarian.Weapon, warden.Weapon);
            Assert.Equal(barbarian.NerveCap, warden.NerveCap);

            foreach (Attr a in Enum.GetValues<Attr>())
                Assert.Equal(barbarian.Attribute(a), warden.Attribute(a));

            foreach (Skill s in Enum.GetValues<Skill>())
                Assert.Equal(barbarian.SkillDie(s), warden.SkillDie(s));
        }


        const string Growing = @",
            ""growth"": [
                { ""id"": ""shield_wall"", ""attribute"": ""might"" },
                { ""id"": ""watchful"", ""skill"": ""insight"" },
                { ""id"": ""spear_drill"", ""ability"": ""hearth_ward"" }
            ]";

        static ClassCard Grower() => Good(Warden.Replace("\"mini\": \"barbarian\"",
                                                        "\"mini\": \"barbarian\"" + Growing));

        [Fact]
        public void AGrowthStepRaisesADieASize()
        {
            Actor before = Grower().Create();
            Actor after = Grower().Create(null, new[] { "shield_wall" });

            Assert.Equal(Die.D8, before.Attribute(Attr.Might));
            Assert.Equal(Die.D10, after.Attribute(Attr.Might));
        }

        [Fact]
        public void AGrowthStepTrainsASkillTheHeroDidNotHave()
        {
            Actor hero = Grower().Create(null, new[] { "watchful" });

            Assert.Equal(Growth.FirstRating, hero.SkillDie(Skill.Insight));
        }

        [Fact]
        public void AGrowthStepUnlocksAnAbilityIntoTheKit()
        {
            ClassCard card = Grower();

            Assert.DoesNotContain("hearth_ward", card.KitAfter(null).Where(a => a == "hearth_ward"));
            Assert.Contains("hearth_ward", card.KitAfter(new[] { "spear_drill" }));
        }

        [Fact]
        public void AConditionAndAGrowthStepComposeInEitherOrder()
        {
            Actor grownThenWinded = Grower().Create(null, new[] { "shield_wall" });
            grownThenWinded.ApplyCondition(Condition.Winded);

            Actor windedThenGrown = Grower().Create();
            windedThenGrown.ApplyCondition(Condition.Winded);
            Grower().Step("shield_wall").ApplyTo(windedThenGrown);

            Assert.Equal(grownThenWinded.Attribute(Attr.Might),
                         windedThenGrown.Attribute(Attr.Might));

            // +1 growth and -1 winded net to the base die
            Assert.Equal(Die.D8, grownThenWinded.Attribute(Attr.Might));
        }

        [Fact]
        public void AStepThisClassDoesNotOfferIsIgnoredRatherThanThrowing()
        {
            Actor hero = Grower().Create(null, new[] { "no_such_step" });

            Assert.Equal(Die.D8, hero.Attribute(Attr.Might));
        }

        [Fact]
        public void AStepHasToRaiseExactlyOneThing()
        {
            Assert.Contains("a growth step has to raise something",
                            Why(Warden.Replace("\"mini\": \"barbarian\"",
                                "\"mini\": \"barbarian\", \"growth\": [ { \"id\": \"empty\" } ]")));

            Assert.Contains("several rewards a campaign cannot hand out separately",
                            Why(Warden.Replace("\"mini\": \"barbarian\"",
                                "\"mini\": \"barbarian\", \"growth\": [ { \"id\": \"both\", " +
                                "\"attribute\": \"might\", \"skill\": \"insight\" } ]")));
        }

        [Fact]
        public void TheProvenanceSaysWhyTheDieIsBigger()
        {
            Assert.Contains("shield_wall", Grower().Step("shield_wall").Source.ToString());
        }

        void Write(string relative, string text)
        {
            string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Content.Kits;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Xunit;

namespace Content.Tests
{
    public sealed class KitTests : IDisposable
    {
        readonly string _root;

        public KitTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "kits-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        const string Pack = "hearthguard";

        static Read<Ability> Parse(string json, string pack = Pack) =>
            AbilityReader.Parse(json, "ability.json", pack);

        static Ability Good(string json, string pack = Pack)
        {
            Read<Ability> read = Parse(json, pack);

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            return read.Value;
        }

        static string Why(string json, string pack = Pack)
        {
            Read<Ability> read = Parse(json, pack);

            Assert.False(read.Ok, "this was supposed to be refused");

            return string.Join("; ", read.Problems.Select(p => p.What));
        }


        [Fact]
        public void TheShippedDoorSurvivesTheRoundTrip()
        {
            Ability written = Good(@"{
                ""id"": ""force_door"",
                ""primitive"": ""check"",
                ""attribute"": ""might"",
                ""skill"": ""blades"",
                ""gear"": true,
                ""versus"": 9,
                ""power"": ""impact"",
                ""pass"": [ ""open"" ],
                ""fail"": [ ""open"", ""rough"", ""recoil"" ]
            }", pack: "");

            Same(SharedKit.ForceDoor, written);
        }

        [Fact]
        public void TheShippedBoltSurvivesTheRoundTrip()
        {
            Ability written = Good(@"{
                ""id"": ""bolt"",
                ""primitive"": ""channel"",
                ""attribute"": ""heart"",
                ""skill"": ""channeling"",
                ""gear"": true,
                ""versus"": ""defense"",
                ""cost"": ""strain"",
                ""target"": ""sight"",
                ""onHit"": [ ""damage"" ]
            }", pack: "");

            Same(SharedKit.Bolt, written);
        }

        static void Same(Ability shipped, Ability written)
        {
            Assert.Equal(shipped.Id, written.Id);
            Assert.Equal(shipped.Primitive, written.Primitive);
            Assert.Equal(shipped.Attribute, written.Attribute);
            Assert.Equal(shipped.Skill, written.Skill);
            Assert.Equal(shipped.UseGear, written.UseGear);
            Assert.Equal(shipped.Against, written.Against);
            Assert.Equal(shipped.AgainstDefense, written.AgainstDefense);
            Assert.Equal(shipped.Magnitude, written.Magnitude);
            Assert.Equal(shipped.Cost, written.Cost);
            Assert.Equal(shipped.Target, written.Target);
            Assert.Equal(shipped.OnHit, written.OnHit);
            Assert.Equal(shipped.OnMiss, written.OnMiss);
            Assert.Equal(shipped.Lands, written.Lands);
        }

        [Fact]
        public void AChannelDefaultsToHeartAndChannelingAndStrain()
        {
            Ability bolt = Good(@"{
                ""id"": ""spark"", ""primitive"": ""channel"",
                ""versus"": ""defense"", ""onHit"": [ ""damage"" ]
            }");

            Assert.Equal(Core.Combat.Channeling.Attribute, bolt.Attribute);
            Assert.Equal(Core.Combat.Channeling.Skill, bolt.Skill);
            Assert.Equal(Cost.Strain, bolt.Cost);
            Assert.Equal(Target.Sight, bolt.Target);
        }

        [Fact]
        public void ACheckHasToNameItsAttribute()
        {
            Assert.Contains("an ability needs a attribute",
                            Why(@"{ ""id"": ""x"", ""primitive"": ""check"", ""versus"": 9,
                                    ""pass"": [ ""open"" ], ""fail"": [ ""recoil"" ] }"));
        }


        [Fact]
        public void APrimitiveThatIsNeitherShapeIsRefusedAsARulesChange()
        {
            string why = Why(@"{ ""id"": ""teleport"", ""primitive"": ""teleport"", ""versus"": 9 }");

            Assert.Contains("this is a rules change, not an ability", why);
            Assert.Contains("channel", why);
            Assert.Contains("check", why);
        }

        [Fact]
        public void AnInventedConditionIsRefusedWithTheLadderThatExists()
        {
            string why = Why(@"{
                ""id"": ""hex"", ""primitive"": ""channel"", ""versus"": ""defense"",
                ""onHit"": [ ""condition"" ], ""condition"": ""cursed""
            }");

            Assert.Contains("is not a condition the engine has", why);
            Assert.Contains("winded", why);
            Assert.Contains("rules change", why);
        }

        [Fact]
        public void AnInventedEffectIsRefusedWithWhatTheEngineCanDo()
        {
            string why = Why(@"{
                ""id"": ""summon"", ""primitive"": ""channel"", ""versus"": ""defense"",
                ""onHit"": [ ""summon_wolf"" ]
            }");

            Assert.Contains("is not something the engine can do", why);
            Assert.Contains("rules change", why);
        }

        [Fact]
        public void AnEffectTheChosenPrimitiveCannotProduceIsRefused()
        {
            string why = Why(@"{
                ""id"": ""knock"", ""primitive"": ""channel"", ""versus"": ""defense"",
                ""onHit"": [ ""open"" ]
            }");

            Assert.Contains("a channel cannot 'open'", why);
        }

        [Fact]
        public void ACheckCannotDealDamageDirectlyBecauseThatIsAChannelsJob()
        {
            Assert.Contains("a check cannot 'damage'",
                            Why(@"{ ""id"": ""x"", ""primitive"": ""check"", ""attribute"": ""might"",
                                    ""versus"": 9, ""pass"": [ ""damage"" ], ""fail"": [ ""recoil"" ] }"));
        }

        [Fact]
        public void ACheckWhoseFailureDoesNothingIsTheRepeatedScenePillarFourForbids()
        {
            string why = Why(@"{
                ""id"": ""pick"", ""primitive"": ""check"", ""attribute"": ""grace"",
                ""skill"": ""larceny"", ""versus"": 9, ""pass"": [ ""open"" ]
            }");

            Assert.Contains("a different scene, not a repeated one", why);
            Assert.Contains("the rules forbid", why);
        }

        [Fact]
        public void AnAbilityThatDoesNothingWhenItLandsIsRefused()
        {
            Assert.Contains("a throw with no outcome",
                            Why(@"{ ""id"": ""x"", ""primitive"": ""channel"", ""versus"": ""defense"" }"));
        }

        [Fact]
        public void AChannelThatLeavesAConditionHasToSayWhich()
        {
            Assert.Contains("does not say which",
                            Why(@"{ ""id"": ""x"", ""primitive"": ""channel"", ""versus"": ""defense"",
                                    ""onHit"": [ ""condition"" ] }"));
        }

        [Fact]
        public void ACheckDoesNotNameItsCondition()
        {
            Assert.Contains("the one it leaves is whichever presses on the attribute it used",
                            Why(@"{ ""id"": ""x"", ""primitive"": ""check"", ""attribute"": ""might"",
                                    ""versus"": 9, ""pass"": [ ""steady"" ], ""fail"": [ ""condition"" ],
                                    ""condition"": ""winded"" }"));
        }

        [Fact]
        public void TheOtherPrimitivesSpellingIsNamedAsThatRatherThanAsATypo()
        {
            Assert.Contains("is how a channel says it",
                            Why(@"{ ""id"": ""x"", ""primitive"": ""check"", ""attribute"": ""might"",
                                    ""versus"": 9, ""onHit"": [ ""open"" ], ""fail"": [ ""recoil"" ] }"));
        }

        [Fact]
        public void ANameInTheFileIsRefusedWithTheCsvLineItShouldHaveBeen()
        {
            string why = Why(@"{
                ""id"": ""x"", ""name"": ""Bolt"", ""primitive"": ""channel"",
                ""versus"": ""defense"", ""onHit"": [ ""damage"" ]
            }");

            Assert.Contains("it is a key, so it can be translated", why);
            Assert.Contains("ability.x.name", why);
        }


        [Fact]
        public void AnAbilityNamesItselfInTheAbilityNamespace()
        {
            Ability ability = Good(@"{
                ""id"": ""hearth_ward"", ""primitive"": ""channel"",
                ""versus"": ""defense"", ""onHit"": [ ""damage"" ]
            }");

            Assert.Equal("ability.hearthguard.hearth_ward.name", ability.NameKey);
            Assert.Equal("ability.hearthguard.hearth_ward.description", ability.DescriptionKey);

            foreach (string key in ability.Keys())
                Assert.Equal(KeyConventions.WellFormed, KeyConventions.Explain(key));
        }

        [Fact]
        public void AbilityIsAKnownNamespace()
        {
            Assert.Contains(KeyConventions.AbilityNs, KeyConventions.Namespaces);
        }


        static PoolResult Throwing(int total)
        {
            int high = System.Math.Min(12, total - 1);

            return PoolResult.From(new[]
            {
                ("attr.might.name", Die.D12, high),
                ("skill.blades.name", Die.D12, total - high),
            });
        }

        [Fact]
        public void LandingAndMissingPickDifferentEffects()
        {
            Ability door = SharedKit.ForceDoor;

            AbilityOutcome landed = door.Read(Throwing(12), impact: 4);
            AbilityOutcome missed = door.Read(Throwing(4), impact: 4);

            Assert.True(landed.Landed);
            Assert.Equal(new[] { Effect.Open }, landed.Effects);

            Assert.False(missed.Landed);
            Assert.True(missed.Does(Effect.Open));
            Assert.True(missed.Does(Effect.Rough));
            Assert.Equal(4, missed.Magnitude);
        }

        [Fact]
        public void AFixedPowerDoesNotVaryWithTheFelt()
        {
            Ability shove = Good(@"{
                ""id"": ""shove"", ""primitive"": ""check"", ""attribute"": ""might"",
                ""skill"": ""brawl"", ""versus"": 9, ""power"": 1,
                ""pass"": [ ""shove"" ], ""fail"": [ ""condition"" ]
            }");

            Assert.Equal(1, shove.Read(Throwing(12), impact: 6).Magnitude);
        }

        [Fact]
        public void AChannelNamesItsConditionAndACheckDerivesOne()
        {
            Ability ward = Good(@"{
                ""id"": ""ward"", ""primitive"": ""channel"", ""versus"": ""defense"",
                ""onHit"": [ ""damage"", ""condition"" ], ""condition"": ""rattled""
            }");

            Assert.Equal(Condition.Rattled, ward.Read(Throwing(20), 3, targetDefense: 9).Condition);

            Ability guard = Good(@"{
                ""id"": ""guard"", ""primitive"": ""check"", ""attribute"": ""might"",
                ""skill"": ""blades"", ""versus"": 9,
                ""pass"": [ ""steady"" ], ""fail"": [ ""condition"" ]
            }");

            Assert.Equal(Condition.Winded, guard.Read(Throwing(3), 2).Condition);
        }

        [Fact]
        public void MeasuringAgainstDefenseUsesTheTargetAndAFlatDifficultyIgnoresIt()
        {
            Assert.Equal(13, SharedKit.Bolt.Difficulty(13));
            Assert.Equal(9, SharedKit.ForceDoor.Difficulty(13));
        }


        [Fact]
        public void StrainIsPaidWhetherItLandsOrNot()
        {
            Actor caster = Caster();

            SharedKit.Bolt.Pay(caster);

            Assert.Equal(1, caster.Strain);
        }

        [Fact]
        public void NerveIsSpentAndRunsOut()
        {
            Ability rally = Good(@"{
                ""id"": ""rally"", ""primitive"": ""check"", ""attribute"": ""heart"",
                ""skill"": ""sway"", ""versus"": 9, ""cost"": ""nerve"",
                ""pass"": [ ""steady"" ], ""fail"": [ ""condition"" ]
            }");

            Actor hero = Caster();
            int had = hero.Nerve;

            Assert.True(rally.Pay(hero));
            Assert.Equal(had - 1, hero.Nerve);

            while (hero.Nerve > 0) hero.SpendNerve();

            Assert.False(rally.Affordable(hero));
            Assert.False(rally.Pay(hero));
        }

        static Actor Caster() =>
            new Actor("caster", maxVigor: 20, defense: 11)
                .With(Attr.Heart, Die.D8)
                .With(Attr.Might, Die.D6)
                .With(Skill.Channeling, Die.D6)
                .With(Skill.Sway, Die.D6)
                .WithWeapon("focus", Die.D6);


        [Fact]
        public void AFolderOfAbilitiesIsReadWholeAndOneBrokenOneDoesNotTakeItDown()
        {
            Write("kits/ward.json", @"{ ""id"": ""ward"", ""primitive"": ""channel"",
                                        ""versus"": ""defense"", ""onHit"": [ ""damage"" ] }");
            Write("kits/broken.json", @"{ ""id"": ""broken"", ""primitive"": ""wish"" }");

            KitBook book = KitBook.Read(Path.Combine(_root, "kits"), Pack);

            Assert.True(book.Has("hearthguard.ward"));
            Assert.Contains(book.Problems, p => p.File == "broken.json");
        }

        [Fact]
        public void AKitFindsThisPacksAbilitiesAndThenTheEnginesOwn()
        {
            Write("kits/ward.json", @"{ ""id"": ""ward"", ""primitive"": ""channel"",
                                        ""versus"": ""defense"", ""onHit"": [ ""damage"" ] }");

            KitBook book = KitBook.Read(Path.Combine(_root, "kits"), Pack);

            Assert.Equal("hearthguard.ward", book.Find("ward").Id);
            Assert.Equal(SharedKit.ForceDoorId, book.Find("force_door").Id);
            Assert.Null(book.Find("nothing_like_this"));
        }

        [Fact]
        public void AMissingFolderIsNotAProblem()
        {
            KitBook book = KitBook.Read(Path.Combine(_root, "nothing-here"), Pack);

            Assert.Empty(book.Ids);
            Assert.Empty(book.Problems);
        }

        void Write(string relative, string text)
        {
            string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }
    }
}

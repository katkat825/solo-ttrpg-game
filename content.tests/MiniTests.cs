using System.Linq;
using Content.Campaigns;
using Content.Minis;
using Content.Schema;
using Xunit;

namespace Content.Tests
{
    public sealed class MiniTests
    {
        const string Pack = "grimdark";

        static Read<MiniManifest> Parse(string json, string pack = Pack) =>
            MiniReader.Parse(json, "oldbones.json", pack);

        static MiniManifest Good(string json, string pack = Pack)
        {
            Read<MiniManifest> read = Parse(json, pack);

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            return read.Value;
        }

        static ContentProblem Bad(string json, string where)
        {
            Read<MiniManifest> read = Parse(json);

            Assert.False(read.Ok);

            ContentProblem problem = read.Problems.FirstOrDefault(p => p.Where == where);

            Assert.True(problem != null,
                        $"expected a problem at '{where}', got: " +
                        string.Join("; ", read.Problems.Select(p => p.ToString())));

            return problem;
        }


        [Fact]
        public void TheSafestGestureIsAVariantOfAShippedMini()
        {
            MiniManifest mini = Good(@"{
                ""id"": ""oldbones"",
                ""variant"": ""rabble"",
                ""tint"": ""#D8CFB8"",
                ""height"": 0.082
            }");

            Assert.Equal("grimdark.oldbones", mini.Id);
            Assert.Equal("rabble", mini.Variant);
            Assert.True(mini.IsVariant);
            Assert.False(mini.HasModel);
            Assert.Equal(0.082f, mini.Height);
            Assert.True(mini.Tint.IsSomething);
        }

        [Fact]
        public void TheIdIsScopedByThePackThatShippedIt()
        {
            Assert.Equal("grimdark.oldbones", Good(@"{ ""id"": ""oldbones"", ""variant"": ""rabble"" }").Id);
        }

        [Fact]
        public void TheBaseGamesOwnMinisKeepTheirUnprefixedForm()
        {
            Assert.Equal("oldbones",
                         Good(@"{ ""id"": ""oldbones"", ""variant"": ""rabble"" }", pack: "").Id);
        }

        [Fact]
        public void ItsDisplayNameIsAKeyDerivedFromTheId()
        {
            Assert.Equal("mini.grimdark.oldbones.name",
                         Good(@"{ ""id"": ""oldbones"", ""variant"": ""rabble"" }").NameKey);
        }

        [Fact]
        public void AMiniThatWritesItsOwnNameIsToldWhereNamesLive()
        {
            ContentProblem problem =
                Bad(@"{ ""id"": ""oldbones"", ""variant"": ""rabble"", ""name"": ""Old Bones"" }",
                    "name");

            Assert.Contains("locale/", problem.What);
        }


        [Fact]
        public void AMiniWithBothAVariantAndAModelIsTwoFiguresUnderOneId()
        {
            Assert.Contains(
                "two figures under one id",
                Bad(@"{ ""id"": ""oldbones"", ""variant"": ""rabble"", ""model"": ""models/x.glb"" }",
                    "model").What);
        }

        [Fact]
        public void AMiniWithNeitherHasNothingToStandOnTheBoard()
        {
            Assert.Contains("nothing to stand on the board",
                            Bad(@"{ ""id"": ""oldbones"" }", "").What);
        }


        [Theory]
        [InlineData("../../../etc/passwd")]
        [InlineData("/etc/passwd")]
        [InlineData(@"C:\windows\system32")]
        [InlineData(@"models\skeleton.glb")]
        [InlineData("models/../../out.glb")]
        public void AModelPathThatLeavesThePackIsRefusedAtTheField(string path)
        {
            Assert.Contains("not inside this pack",
                            Bad($@"{{ ""id"": ""oldbones"", ""model"": ""{path.Replace(@"\", @"\\")}"" }}",
                                "model").What);
        }

        [Fact]
        public void AModelInsideThePackIsFine()
        {
            Assert.Equal("models/skeleton.glb",
                         Good(@"{ ""id"": ""oldbones"", ""model"": ""models/skeleton.glb"" }").Model);
        }


        [Fact]
        public void AHeightOnItsOwnIsAHeightAndNotAnError()
        {
            MiniManifest mini = Good(@"{ ""id"": ""oldbones"", ""variant"": ""rabble"", ""height"": 0.082 }");

            Assert.Null(mini.Fit);
            Assert.Equal(0.082f, mini.Height);
        }

        [Fact]
        public void FittingToTheCellAndAlsoStatingAHeightIsAContradictionAndIsSaidSo()
        {
            Assert.Contains("fitted to the cell",
                            Bad(@"{ ""id"": ""oldbones"", ""model"": ""a.glb"", ""fit"": ""cell"",
                                    ""height"": 0.08 }", "height").What);
        }

        [Fact]
        public void FittingByHeightWithNoHeightIsRefused()
        {
            Assert.Contains("0.075",
                            Bad(@"{ ""id"": ""oldbones"", ""model"": ""a.glb"", ""fit"": ""height"" }",
                                "height").What);
        }

        [Fact]
        public void AWayOfFittingNobodyHasHeardOfIsOfferedTheTwoThatExist()
        {
            Assert.Contains("cell",
                            Bad(@"{ ""id"": ""oldbones"", ""model"": ""a.glb"", ""fit"": ""squish"" }",
                                "fit").What);
        }

        [Fact]
        public void AFigureTallerThanTheRoomIsAUnitMixUpAndIsCaughtAsOne()
        {
            Assert.Contains("METRES",
                            Bad(@"{ ""id"": ""oldbones"", ""variant"": ""rabble"", ""height"": 75 }",
                                "height").What);
        }


        [Fact]
        public void ATintIsSixHexDigits()
        {
            Tint tint = Good(@"{ ""id"": ""oldbones"", ""variant"": ""rabble"", ""tint"": ""#D8CFB8"" }").Tint;

            Assert.True(tint.IsSomething);
            Assert.Equal("#D8CFB8", tint.ToString());
        }

        [Fact]
        public void APaletteNameIsRefusedAndTheRefusalSaysWhy()
        {
            Assert.Contains("palette.ps1",
                            Bad(@"{ ""id"": ""oldbones"", ""variant"": ""rabble"", ""tint"": ""bone"" }",
                                "tint").What);
        }


        [Fact]
        public void ClipsAreKeyedByWhatThePieceIsDoing()
        {
            MiniManifest mini = Good(@"{
                ""id"": ""oldbones"",
                ""model"": ""models/skeleton.glb"",
                ""clips"": { ""move"": ""Walk"", ""topple"": ""Death_A"" }
            }");

            Assert.Equal("Walk", mini.Clips[Motion.Move]);
            Assert.Equal("Death_A", mini.Clips[Motion.Topple]);
            Assert.False(mini.Clips.ContainsKey(Motion.Strike));
        }

        [Fact]
        public void SomethingAPieceDoesNotDoIsOfferedTheOnesItDoes()
        {
            ContentProblem problem =
                Bad(@"{ ""id"": ""oldbones"", ""model"": ""a.glb"", ""clips"": { ""dance"": ""Boogie"" } }",
                    "clips.dance");

            foreach (string motion in new[] { "placed", "move", "strike", "wobble", "topple" })
                Assert.Contains(motion, problem.What);
        }

        // clip names are the model's own, deliberately not held to id grammar
        [Fact]
        public void AClipMayBeCalledWhateverTheExporterCalledIt()
        {
            Assert.Equal("Armature|Death_A",
                         Good(@"{ ""id"": ""x"", ""model"": ""a.glb"",
                                  ""clips"": { ""topple"": ""Armature|Death_A"" } }")
                             .Clips[Motion.Topple]);
        }

        [Fact]
        public void FoleyIsAFolderAndACapturedPathIsHeldToTheSameRule()
        {
            Assert.Contains("FOLDER of wavs",
                            Bad(@"{ ""id"": ""x"", ""model"": ""a.glb"",
                                    ""foley"": { ""placed"": ""../elsewhere"" } }",
                                "foley.placed").What);
        }

        [Fact]
        public void AFoleyFolderInsideThePackIsFine()
        {
            Assert.Equal("audio/bones/placed",
                         Good(@"{ ""id"": ""x"", ""model"": ""a.glb"",
                                  ""foley"": { ""placed"": ""audio/bones/placed"" } }")
                             .Foley[Motion.Placed]);
        }


        [Fact]
        public void AFieldAMiniDoesNotHaveIsATypoAndIsNamed()
        {
            Assert.Contains("a mini has no 'stats'",
                            Bad(@"{ ""id"": ""x"", ""variant"": ""rabble"", ""stats"": 3 }",
                                "stats").What);
        }

        [Fact]
        public void AMiniCannotCarryAStatblockField()
        {
            Assert.False(Parse(@"{ ""id"": ""x"", ""variant"": ""rabble"", ""vigor"": 12 }").Ok);
        }

        [Fact]
        public void AnIdWithADotInItIsRefusedBecauseItBecomesAKeySegment()
        {
            Assert.Contains("lowercase a-z",
                            Bad(@"{ ""id"": ""old.bones"", ""variant"": ""rabble"" }", "id").What);
        }

        [Fact]
        public void SomethingThatIsNotJsonAtAllIsRefusedWithALine()
        {
            Read<MiniManifest> read = Parse("{ not json");

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.StartsWith("this is not JSON"));
        }


        [Fact]
        public void TheBaseGameShipsFourMinisAndTheyAreNotNamespaced()
        {
            Assert.Contains(SharedMinis.Rabble, SharedMinis.Catalogue.Ids);
            Assert.All(SharedMinis.All, m => Assert.False(ContentId.IsScoped(m.Id)));
        }

        [Fact]
        public void AShippedMiniHasNeitherAVariantNorAModel()
        {
            Assert.All(SharedMinis.All, m => Assert.True(m.IsShipped));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Encounters;
using Content.Schema;
using Xunit;

namespace Content.Tests
{
    public sealed class PackageTests : IDisposable
    {
        readonly string _root;

        public PackageTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "campaigns-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }


        string Folder(string campaign = Ashfall)
        {
            string folder = Path.Combine(_root, campaign);

            Directory.CreateDirectory(folder);

            return folder;
        }

        const string Ashfall = "ashfall";

        void Write(string campaign, string relative, string text)
        {
            string path = Path.Combine(Folder(campaign), relative.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        static string Manifest(string id = Ashfall, string extra = "") => $@"{{
            ""id"": ""{id}"",
            ""format"": {ContentFormat.Current},
            ""engine"": ""{Core.EngineVersion.Current}"",
            ""chapters"": [ {{ ""id"": ""one"", ""encounters"": [ ""yard"" ] }} ]
            {extra}
        }}";

        const string Ghoul = @"{
            ""id"": ""ghoul"",
            ""tier"": ""rival"",
            ""vigor"": 10,
            ""defense"": 11,
            ""attributes"": { ""might"": ""d8"" },
            ""gear"": { ""id"": ""claw"", ""die"": ""d6"" }
        }";

        const string Yard = @"+-+-+-+-+
|. . 1 .|
+ + + + +
|@ . . .|
+-+-+-+-+
";

        const string Encounter = @"{
            ""id"": ""yard"",
            ""map"": ""yard"",
            ""placements"": [ { ""slot"": 1, ""monster"": ""ghoul"" } ],
            ""triggers"": [ { ""when"": ""cleared"", ""then"": ""ends"" } ],
            ""cues"": [ { ""when"": ""entered"", ""cue"": ""it_opens"" } ]
        }";

        void WholeCampaign(string id = Ashfall)
        {
            Write(id, ManifestReader.FileName, Manifest(id));
            Write(id, "monsters/ghoul.json", Ghoul);
            Write(id, "maps/yard.map", Yard);
            Write(id, "encounters/yard.json", Encounter);
        }

        Package Read(string campaign = Ashfall) => Package.Read(Path.Combine(_root, campaign));

        static string Why(Package package) =>
            string.Join(" | ", package.Problems.Select(p => p.ToString()));


        [Fact]
        public void AFolderWithEveryPartOfACampaignInItLoadsClean()
        {
            WholeCampaign();

            Package package = Read();

            Assert.False(package.Failed, Why(package));
            Assert.Empty(package.Problems);
            Assert.Equal(Ashfall, package.Id);
            Assert.True(package.Monsters.Has("ashfall.ghoul"));
            Assert.True(package.Maps.ContainsKey("yard"));
            Assert.True(package.Encounters.Has("yard"));
        }

        [Fact]
        public void AnEncounterSaysWhoStandsOnWhichSlotAndTheMapSaysWhere()
        {
            WholeCampaign();

            EncounterPlan plan = Read().Encounters.Of("yard");

            Assert.Equal("yard", plan.Map);
            Assert.Equal(1, Assert.Single(plan.Placements).Slot);

            Assert.Equal(new[] { "ashfall.ghoul" }, plan.Roster(Ashfall));
        }

        [Fact]
        public void ATriggerAndACueSurviveTheRoundTripEvenThoughNothingRunsThemYet()
        {
            WholeCampaign();

            EncounterPlan plan = Read().Encounters.Of("yard");

            Trigger trigger = Assert.Single(plan.Triggers);
            Assert.Equal(When.Cleared, trigger.WhenIt);
            Assert.Equal(Then.Ends, trigger.ThenDo);

            Cue cue = Assert.Single(plan.Cues);
            Assert.Equal(When.Entered, cue.WhenIt);
            Assert.Equal("it_opens", cue.Id);
        }

        [Fact]
        public void ACampaignsTitleIsAKeyDerivedFromItsIdAndNotAFieldInTheFile()
        {
            WholeCampaign();

            Manifest manifest = Read().Manifest;

            Assert.Equal("campaign.ashfall.name", manifest.NameKey);
            Assert.Equal("campaign.ashfall.description", manifest.DescriptionKey);
            Assert.Contains("quest.ashfall.one.title", manifest.Keys());
        }


        [Fact]
        public void AFolderWithNoManifestIsAFolderAndNotACampaign()
        {
            Write(Ashfall, "monsters/ghoul.json", Ghoul);

            Package package = Read();

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.What.Contains("campaign.json"));

            Assert.Null(package.Monsters);
        }

        [Fact]
        public void AManifestThatIsNotJsonNamesTheLineAndStopsTheFolder()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName, "{ \"id\": \"ashfall\",");

            Package package = Read();

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.Line > 0);
        }

        [Fact]
        public void ABrokenCampaignDoesNotTakeTheOneBesideItDown()
        {
            WholeCampaign();
            WholeCampaign("emberfall");
            Write("emberfall", ManifestReader.FileName, "not json at all");

            Assert.False(Read().Failed);
            Assert.True(Read("emberfall").Failed);
        }

        [Fact]
        public void ReadingAFolderThatIsNotThereIsAProblemAndNotAnException()
        {
            Package package = Package.Read(Path.Combine(_root, "never_installed"));

            Assert.True(package.Failed);
            Assert.NotEmpty(package.Problems);
        }

        [Fact]
        public void AFolderThatIsMovedOutLeavesTheShelfAndComesBackWhenItReturns()
        {
            WholeCampaign();

            Assert.False(Read().Failed);

            string folder = Path.Combine(_root, Ashfall);
            string aside = Path.Combine(_root, "..", "aside-" + Guid.NewGuid().ToString("N"));

            Directory.Move(folder, aside);

            try
            {
                Assert.True(Read().Failed);

                Directory.Move(aside, folder);
            }
            finally
            {
                if (Directory.Exists(aside)) Directory.Delete(aside, recursive: true);
            }

            Assert.False(Read().Failed);
        }


        [Fact]
        public void TheIdAndTheFolderNameHaveToMatch()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName, Manifest("emberfall"));

            Package package = Read();

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.Where == "id" && p.What.Contains("folder"));
        }

        [Fact]
        public void ATitleInTheManifestIsRefusedWithWhereItActuallyGoes()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName,
                  Manifest(extra: @", ""title"": ""Ashfall"""));

            Package package = Read();

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.Where == "title" && p.What.Contains("locale"));
        }

        [Fact]
        public void ACampaignWrittenForALaterFormatIsRefusedAndSaysSoInOneSentence()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName,
                  Manifest().Replace($"\"format\": {ContentFormat.Current}",
                                     $"\"format\": {ContentFormat.Current + 1}"));

            Package package = Read();

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.What.Contains("update the game"));
        }

        [Fact]
        public void ACampaignNeedingALaterEngineIsRefused()
        {
            Version later = new Version(Core.EngineVersion.Current.Major + 1, 0);

            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName,
                  Manifest().Replace($"\"engine\": \"{Core.EngineVersion.Current}\"",
                                     $"\"engine\": \"{later}\""));

            Package package = Read();

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.Where == "engine");
        }

        [Fact]
        public void ADependencyThatIsNotAPackIdIsRefused()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName,
                  Manifest(extra: @", ""dependencies"": [ ""somebody.minis"" ]"));

            Package package = Read();

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.Where == "dependencies[0]");
        }

        [Fact]
        public void ADependencyOnAPackIsReadAndCarried()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName,
                  Manifest(extra: @", ""dependencies"": [ ""grimdark"" ]"));

            Package package = Read();

            Assert.False(package.Failed, Why(package));
            Assert.Equal(new[] { "grimdark" }, package.Manifest.Dependencies);
        }

        [Fact]
        public void AnEmptyDependenciesListIsFine()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName, Manifest(extra: @", ""dependencies"": []"));

            Assert.False(Read().Failed, Why(Read()));
        }

        [Fact]
        public void ACampaignThatStartsAtNoChapterIsRefused()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName, Manifest(extra: @", ""start"": ""two"""));

            Package package = Read();

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.Where == "start");
        }

        [Fact]
        public void APreviewImageThatIsNotThereIsAHoleInTheShelfEntry()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName, Manifest(extra: @", ""preview"": ""box.png"""));

            Package package = Read();

            Assert.False(package.Failed);
            Assert.Contains(package.Problems, p => p.Where == "preview");
        }


        [Fact]
        public void AnEncounterOnAMapTheCampaignDoesNotShipIsNamed()
        {
            WholeCampaign();
            Write(Ashfall, "encounters/yard.json", Encounter.Replace(@"""map"": ""yard""",
                                                                    @"""map"": ""crypt"""));

            Package package = Read();

            Assert.Contains(package.Problems, p => p.Where == "map" && p.What.Contains("crypt"));
        }

        [Fact]
        public void AnEncounterPlacingAMonsterTheCampaignDoesNotShipIsNamed()
        {
            WholeCampaign();
            Write(Ashfall, "encounters/yard.json", Encounter.Replace(@"""monster"": ""ghoul""",
                                                                    @"""monster"": ""wight"""));

            Package package = Read();

            Assert.Contains(package.Problems,
                            p => p.Where == "placements[0].monster" && p.What.Contains("wight"));
        }

        [Fact]
        public void APlacementOnASlotTheMapNeverDrewIsNamed()
        {
            WholeCampaign();
            Write(Ashfall, "encounters/yard.json", Encounter.Replace(@"""slot"": 1", @"""slot"": 4"));

            Package package = Read();

            Assert.Contains(package.Problems, p => p.Where == "placements[0].slot");
        }

        [Fact]
        public void AChapterNamingAnEncounterThatIsNotThereIsNamed()
        {
            WholeCampaign();
            Write(Ashfall, ManifestReader.FileName,
                  Manifest().Replace(@"""encounters"": [ ""yard"" ]", @"""encounters"": [ ""crypt"" ]"));

            Package package = Read();

            Assert.Contains(package.Problems, p => p.Where.StartsWith("chapters[0].encounters["));
        }

        [Fact]
        public void AnEncounterNoChapterNamesIsNamed()
        {
            WholeCampaign();
            Write(Ashfall, "encounters/crypt.json", Encounter.Replace(@"""id"": ""yard""",
                                                                     @"""id"": ""crypt"""));

            Package package = Read();

            Assert.Contains(package.Problems, p => p.File.EndsWith("crypt.json") && p.Where == "id");
        }

        [Fact]
        public void AMonsterDroppingAnItemTheCampaignDoesNotShipIsNamed()
        {
            WholeCampaign();
            Write(Ashfall, "monsters/ghoul.json",
                  Ghoul.Replace(@"""gear"": { ""id"": ""claw"", ""die"": ""d6"" }",
                                @"""gear"": { ""id"": ""claw"", ""die"": ""d6"" },
                                  ""loot"": [ { ""item"": ""cold_iron_axe"", ""weight"": 1 } ]"));

            Package package = Read();

            Assert.Contains(package.Problems,
                            p => p.Where == "loot" && p.What.Contains("cold_iron_axe"));
        }

        [Fact]
        public void AMonsterDroppingAnItemTheCampaignDoesShipIsFine()
        {
            WholeCampaign();
            Write(Ashfall, "items/cold_iron_axe.json",
                  @"{ ""id"": ""cold_iron_axe"", ""die"": ""d8"", ""supports"": ""blades"" }");
            Write(Ashfall, "monsters/ghoul.json",
                  Ghoul.Replace(@"""gear"": { ""id"": ""claw"", ""die"": ""d6"" }",
                                @"""gear"": { ""id"": ""claw"", ""die"": ""d6"" },
                                  ""loot"": [ { ""item"": ""cold_iron_axe"", ""weight"": 1 } ]"));

            Package package = Read();

            Assert.False(package.Failed, Why(package));
            Assert.Empty(package.Problems);
        }

        [Fact]
        public void EveryProblemInTheFolderIsReportedInOneRead()
        {
            WholeCampaign();
            Write(Ashfall, "encounters/yard.json",
                  Encounter.Replace(@"""map"": ""yard""", @"""map"": ""crypt""")
                           .Replace(@"""monster"": ""ghoul""", @"""monster"": ""wight"""));
            Write(Ashfall, "monsters/broken.json", "{ \"id\": \"broken\" }");

            Package package = Read();

            Assert.True(package.Problems.Count >= 3, Why(package));
        }
    }
}

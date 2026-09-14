using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Encounters;
using Content.Schema;
using Xunit;

namespace Content.Tests
{
    // A CAMPAIGN FOLDER, READ WHOLE (CONTENT_PIPELINE.md P4).
    //
    // Written against a real folder on a real disk rather than against strings, because half of
    // what P4 promises is about files being where they say they are: a manifest whose id matches
    // its folder, an encounter naming a map in `maps/`, a monster dropping an item the campaign
    // ships. None of that can be tested with a JSON literal, and all of it is what breaks while
    // authoring.
    //
    // THE ISOLATION BOUNDARY IS THE THING MOST WORTH PINNING. A folder with a corrupt manifest has
    // to come back as a value that says so - not an exception, not a half-loaded campaign, and not
    // silence. Every test below that reaches for `Failed` is testing the behaviour that keeps one
    // bad subscribed item from taking the other seven down with it.
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
            try { Directory.Delete(_root, recursive: true); } catch { /* a temp folder */ }
        }

        // ---- a campaign, built one file at a time ----

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

        // 4 x 2, one spawn slot at (1, 0) and the hero at (0, 1)
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

        // the smallest campaign that loads clean, which every test below either reads or breaks
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

        // ---- the whole thing ----

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

            // scoped on the way out, because a fight looks monsters up in one shared roster
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

        // ---- the isolation boundary ----

        [Fact]
        public void AFolderWithNoManifestIsAFolderAndNotACampaign()
        {
            Write(Ashfall, "monsters/ghoul.json", Ghoul);

            Package package = Read();

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.What.Contains("campaign.json"));

            // AND NOTHING IN IT IS IN PLAY. The monster is sitting right there and is not loaded,
            // which is the whole of the boundary: all of a campaign or none of it
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

        // MOVE THE FOLDER OUT AND IT LEAVES THE SHELF; MOVE IT BACK AND IT RETURNS - P4's verify,
        // and the reason it is worth a test is that nothing is cached anywhere. A loader that held
        // a campaign after its folder was gone would pass every other test in this file
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

        // ---- the manifest's own fields ----

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

        // WAS "dependencies IS RESERVED AND SAYS SO" UNTIL PHASE A. The field was refused
        // non-empty from P4 until A4 made it live; what is still refused is a dependency that is
        // not a PACK ID, because `somebody.minis` is two segments and a pack id is one
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

        // NOTHING IN THE FOLDER KNOWS WHETHER `grimdark` IS INSTALLED, and that is deliberate -
        // it is a question about the shelf, and `ShelfTests` is where it is asked
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

        // ---- the questions no single file can answer ----

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

        // AN ENCOUNTER NO CHAPTER NAMES IS A FILE THAT WILL NEVER BE PLAYED, which is a mistake
        // that is completely invisible until somebody wonders why a room never came up
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

        // EVERY PROBLEM AT ONCE - the thing that makes authoring a list to work through rather
        // than a load-fix-load loop (ContentProblem). Three separate mistakes, one read
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

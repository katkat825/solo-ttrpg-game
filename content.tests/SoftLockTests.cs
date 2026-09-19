using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Entities;
using Content.Places;
using Content.Quests;
using Content.Schema;
using Content.World;
using Xunit;

namespace Content.Tests
{
    // THE SOFT-LOCK AUTHOR WARNING (V1).
    //
    // The runtime consequence was already right: kill the man who set the errand and the errand
    // reads Failed rather than sitting in the log forever. What is tested here is the half an
    // author sees before a player does - the validator walking the quest graph and saying which
    // killing strands which turn-in, and saying it as a caution that never stops a campaign
    // loading.
    //
    // The case worth catching is the QUIET one. An author who wrote 'unless bob.dead' knew what
    // they were doing; an author whose turn-in waits on 'bob.spoken' and who never wrote 'dead'
    // anywhere has built a quest that can be made impossible and never reads as failed, and
    // nothing in the files looks wrong.
    public sealed class SoftLockTests : IDisposable
    {
        readonly string _root;

        public SoftLockTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "softlock-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        const string Campaign = "hamlet";

        string Folder => Path.Combine(_root, Campaign);

        void Write(string relative, string text)
        {
            string path = Path.Combine(Folder, relative.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        const string Square = @"
+-+-+-+-+-+
|. 1 . 2 .|
+ + + + + +
|. . . . .|
+ + + + + +
|. 3 . 4 .|
+ + + + + +
|@ . . . .|
+-+-+-+-+-+
";

        // a hamlet with a man you may talk to and kill, a chest you may not kill, and whatever
        // quest the test is about
        Package World(string quest, string extra = "")
        {
            Write(ManifestReader.FileName, $@"{{
                ""id"": ""{Campaign}"",
                ""format"": {ContentFormat.Current},
                ""engine"": ""{Core.EngineVersion.Current}"",
                ""chapters"": [ {{ ""id"": ""one"", ""places"": [ ""square"" ] }} ]
            }}");

            Write("maps/square.map", Square);

            Write("monsters/bob_at_bay.json", @"{
                ""id"": ""bob_at_bay"", ""tier"": ""rival"", ""vigor"": 6, ""defense"": 9,
                ""attributes"": { ""might"": ""d8"" },
                ""gear"": { ""id"": ""stool"", ""die"": ""d6"" }
            }");

            Write("items/rock.json", @"{ ""id"": ""rock"", ""die"": ""d4"" }");

            Write("dialogue/hamlet.yarn", @"title: bob_at_the_well
speaker: wolf
---
He has been waiting for somebody all morning. #line:waiting_all_morning
===
");

            Write("entities/bob.json", @"{
                ""id"": ""bob"",
                ""monster"": ""bob_at_bay"",
                ""can"": { ""talk"": ""bob_at_the_well"", ""attack"": null }
            }");

            Write("entities/chest.json", @"{
                ""id"": ""chest"",
                ""can"": { ""search"": null },
                ""loot"": [ { ""item"": ""rock"", ""weight"": 1 } ]
            }");

            Write("places/square.json", @"{
                ""id"": ""square"",
                ""map"": ""square"",
                ""standing"": [
                    { ""slot"": 1, ""entity"": ""bob"" },
                    { ""slot"": 2, ""entity"": ""chest"" }
                ]
            }");

            Write("quests/the_errand.json", quest);

            if (extra.Length > 0) Write(extra.Split('|')[0], extra.Split('|')[1]);

            return Package.Read(Folder);
        }

        static string Said(Package package) =>
            string.Join("; ", package.Problems.Select(p => p.ToString()));


        // ---- what the pass actually derives ----------------------------------------------------

        [Fact]
        public void AVerbOnSomebodyYouCanKillIsAFactThatDiesWithThem()
        {
            Package package = World(@"{ ""id"": ""the_errand"",
                                        ""done"": { ""when"": [ ""bob.spoken"" ] } }");

            FactSources sources = FactSources.Of(package.Entities, package.Places,
                                                 package.Quests, package.Roads, package.Maps);

            Assert.Equal("bob", sources.OnlyOnTheLivingOf("bob.spoken"));

            // the swing is what writes this one, so it outlives him by definition
            Assert.Equal("", sources.OnlyOnTheLivingOf("bob.dead"));

            // a chest's verb dies with the chest in exactly the same way; what makes it harmless
            // is that nobody may kill a chest, and that question belongs to Strandable
            Assert.Equal("chest", sources.OnlyOnTheLivingOf("chest.looted"));

            // and a fact nothing writes is nobody's
            Assert.Equal("", sources.OnlyOnTheLivingOf("moon.risen"));
        }

        [Fact]
        public void OneSourceThatIsNotAPersonKeepsTheFactReachablePastEverybody()
        {
            Package package = World(
                @"{ ""id"": ""the_errand"", ""done"": { ""when"": [ ""bob.spoken"" ] } }",
                "places/square.json|" + @"{
                    ""id"": ""square"", ""map"": ""square"",
                    ""standing"": [
                        { ""slot"": 1, ""entity"": ""bob"" },
                        { ""slot"": 2, ""entity"": ""chest"" }
                    ],
                    ""triggers"": [
                        { ""when"": ""entered"", ""then"": ""set"", ""sets"": ""bob.spoken"" }
                    ]
                }");

            FactSources sources = FactSources.Of(package.Entities, package.Places,
                                                 package.Quests, package.Roads, package.Maps);

            Assert.True(sources.Writable("bob.spoken"));
            Assert.Equal("", sources.OnlyOnTheLivingOf("bob.spoken"));
            Assert.Empty(package.Cautions);
        }

        [Fact]
        public void TheWritableSetIsStillTheSameSetTheReachabilityPassNeeds()
        {
            Package package = World(@"{ ""id"": ""the_errand"",
                                        ""done"": { ""when"": [ ""bob.spoken"" ] } }");

            FactSources sources = FactSources.Of(package.Entities, package.Places,
                                                 package.Quests, package.Roads, package.Maps);

            Assert.True(sources.Writable("bob.spoken"));
            Assert.True(sources.Writable("bob.dead"));
            Assert.True(sources.Writable("chest.looted"));
            Assert.True(sources.Writable("square.visited"));
            Assert.True(sources.Writable("the_errand.accepted"));
            Assert.False(sources.Writable("moon.risen"));
        }


        // ---- the warning itself ----------------------------------------------------------------

        [Fact]
        public void ATurnInOnlyALivingManCanWriteIsCautioned()
        {
            Package package = World(@"{ ""id"": ""the_errand"",
                                        ""done"": { ""when"": [ ""bob.spoken"" ] } }");

            ContentProblem caution = Assert.Single(package.Cautions);

            Assert.Equal("quests/the_errand.json", caution.File);
            Assert.Equal("done.when", caution.Where);
            Assert.Contains("bob.spoken", caution.What);
            Assert.Contains("attackable", caution.What);

            // no 'failed' clause anywhere, so the log would sit there forever
            Assert.Contains("sit there forever", caution.What);

            Assert.True(package.Clean, "a caution must never stop a campaign loading");
        }

        [Fact]
        public void AQuestThatSaysWhatFailingLooksLikeIsStillCautionedAndSaidDifferently()
        {
            Package package = World(@"{
                ""id"": ""the_errand"",
                ""done"": { ""when"": [ ""bob.spoken"" ] },
                ""failed"": { ""when"": [ ""bob.dead"" ] }
            }");

            ContentProblem caution = Assert.Single(package.Cautions);

            Assert.Contains("the log will say so", caution.What);
            Assert.DoesNotContain("sit there forever", caution.What);
        }

        [Fact]
        public void AnAuthorWhoWroteUnlessDeadIsWarnedOnceAndNotTwice()
        {
            Package package = World(@"{
                ""id"": ""the_errand"",
                ""done"": { ""when"": [ ""bob.spoken"" ], ""unless"": [ ""bob.dead"" ] },
                ""failed"": { ""when"": [ ""bob.dead"" ] }
            }");

            ContentProblem caution = Assert.Single(package.Cautions);

            // the clause the author wrote is the one they are pointed at
            Assert.Equal("done.unless", caution.Where);
            Assert.Contains("once 'bob' is dead", caution.What);
        }

        [Fact]
        public void ATurnInNobodyHasToBeAliveForIsNotCautioned()
        {
            Package package = World(@"{ ""id"": ""the_errand"",
                                        ""done"": { ""when"": [ ""chest.looted"" ] } }");

            Assert.Empty(package.Cautions);
            Assert.True(package.Clean, Said(package));
        }

        [Fact]
        public void AQuestWaitingOnADeathIsNotAQuestADeathCanStrand()
        {
            Package package = World(@"{ ""id"": ""the_errand"",
                                        ""done"": { ""when"": [ ""bob.dead"" ] } }");

            Assert.Empty(package.Cautions);
        }

        [Fact]
        public void BeingUnableToBeOfferedAQuestIsNotBeingStrandedByIt()
        {
            // 'offered' waits on the man too, and that is not a soft lock - it is a quest you
            // never had. Warning about it would bury the one that matters.
            Package package = World(@"{
                ""id"": ""the_errand"",
                ""offered"": { ""when"": [ ""bob.spoken"" ] },
                ""done"": { ""when"": [ ""chest.looted"" ] }
            }");

            Assert.Empty(package.Cautions);
        }

        [Fact]
        public void AManYouAreNotAllowedToKillStrandsNothing()
        {
            Package package = World(
                @"{ ""id"": ""the_errand"", ""done"": { ""when"": [ ""bob.spoken"" ] } }",
                "entities/bob.json|" + @"{
                    ""id"": ""bob"",
                    ""can"": { ""talk"": ""bob_at_the_well"" }
                }");

            Assert.Empty(package.Cautions);
            Assert.True(package.Clean, Said(package));
        }

        [Fact]
        public void TheFactTheReachabilityPassAlreadyRefusedIsNotSaidTwice()
        {
            Package package = World(@"{ ""id"": ""the_errand"",
                                        ""done"": { ""when"": [ ""moon.risen"" ] } }");

            Assert.Contains(package.Faults, p => p.What.Contains("ever makes 'moon.risen' true"));
            Assert.Empty(package.Cautions);
        }


        // ---- the shipped worked case -----------------------------------------------------------

        // saltmarch's own caution has its own test in ShippedCampaignTests; this is the other
        // half of it - that no campaign on the shelf strands a quest it never says can fail.
        [Fact]
        public void NoShippedCampaignStrandsAQuestWithoutSayingSo()
        {
            foreach (string folder in Directory.GetDirectories(Shipped()))
            {
                Package package = Package.Read(folder);

                if (package.Failed) continue;

                FactSources sources = FactSources.Of(package.Entities, package.Places,
                                                     package.Quests, package.Roads, package.Maps);

                foreach (Quest quest in package.Quests.All)
                    foreach (Strandable stranded in Strandable.In(quest, package.Entities, sources))
                        Assert.True(stranded.SaysHowItFails, $"{package.Id}: {stranded}");
            }
        }

        static string Shipped()
        {
            var here = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (here != null && !Directory.Exists(Path.Combine(here.FullName, "campaigns")))
                here = here.Parent;

            Assert.NotNull(here);

            return Path.Combine(here.FullName, "campaigns");
        }
    }
}

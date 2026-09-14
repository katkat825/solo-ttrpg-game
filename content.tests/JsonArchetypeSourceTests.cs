using System;
using System.IO;
using System.Linq;
using Content.Monsters;
using Core.Characters;
using Core.Dice;
using Xunit;

namespace Content.Tests
{
    // a folder of monsters, and what happens when one of them is broken (CONTENT_PIPELINE.md P0)
    //
    // THE INTERESTING CASE IS THE MIXED FOLDER. Three good files and one bad one has to load three
    // monsters and report one problem - not zero monsters and an exception - because that is the
    // isolation boundary in miniature: one bad file fails alone and named, and everything else
    // still works. A loader that refuses the whole folder is a loader that makes a stranger's typo
    // into your crash.
    public sealed class JsonArchetypeSourceTests : IDisposable
    {
        readonly string _folder;

        public JsonArchetypeSourceTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "statblocks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_folder, recursive: true); } catch { /* a temp folder */ }
        }

        void Write(string name, string json) => File.WriteAllText(Path.Combine(_folder, name), json);

        static string Monster(string id, string tier = "rival") => $@"{{
            ""id"": ""{id}"",
            ""tier"": ""{tier}"",
            ""vigor"": 8,
            ""defense"": 11,
            ""attributes"": {{ ""might"": ""d8"" }},
            ""gear"": {{ ""id"": ""claw"", ""die"": ""d6"" }}
        }}";

        JsonArchetypeSource Read(string campaign = "ashfall") =>
            JsonArchetypeSource.Read(_folder, campaign);

        // ---- a folder of them ----

        [Fact]
        public void EveryFileInTheFolderIsAMonster()
        {
            Write("ghoul.json", Monster("ghoul"));
            Write("wight.json", Monster("wight", "dread"));

            JsonArchetypeSource source = Read();

            Assert.Empty(source.Problems);
            Assert.Equal(2, source.Ids.Count);
            Assert.True(source.Has("ashfall.ghoul"));
            Assert.True(source.Has("ashfall.wight"));
            Assert.Equal(Tier.Dread, source.Create("ashfall.wight").Tier);
        }

        [Fact]
        public void AndItIsAnIArchetypeSourceLikeAnyOther()
        {
            Write("ghoul.json", Monster("ghoul"));

            IArchetypeSource source = Read();

            Actor ghoul = source.Create("ashfall.ghoul");

            Assert.Equal(Die.D8, ghoul.Attribute(Attr.Might));
            Assert.Equal("gear.claw.name", ghoul.WeaponKey);
        }

        // ONE BAD FILE FAILS ALONE. The whole argument for the isolation boundary, at the scale of
        // a folder
        [Fact]
        public void ABrokenFileIsReported_AndTheRestStillLoad()
        {
            Write("ghoul.json", Monster("ghoul"));
            Write("broken.json", @"{ ""id"": ""thing"", ""attributes"": { ""might"": ""d7"" } }");
            Write("wight.json", Monster("wight"));

            JsonArchetypeSource source = Read();

            Assert.Equal(2, source.Ids.Count);
            Assert.NotEmpty(source.Problems);
            Assert.All(source.Problems, p => Assert.Equal("broken.json", p.File));
        }

        [Fact]
        public void TwoStatblocksWithOneIdIsReported()
        {
            Write("a.json", Monster("ghoul"));
            Write("b.json", Monster("ghoul"));

            JsonArchetypeSource source = Read();

            Assert.Single(source.Ids);
            Assert.Contains(source.Problems, p => p.What.Contains("already the id"));
        }

        // anything that is not a .json is somebody's notes, their editor's backup, or a folder -
        // and none of those is a monster
        [Fact]
        public void NothingButJsonIsRead()
        {
            Write("ghoul.json", Monster("ghoul"));
            Write("notes.txt", "the ghoul should probably be tougher");
            Write("ghoul.json.bak", "{ this is not json at all");

            JsonArchetypeSource source = Read();

            Assert.Single(source.Ids);
            Assert.Empty(source.Problems);
        }

        // A GAME WITH NO CAMPAIGNS INSTALLED HAS TO BOOT, and a campaign with no monsters of its
        // own is a perfectly good campaign - it can use the engine's roster
        [Fact]
        public void AMissingFolderIsEmptyRatherThanAProblem()
        {
            JsonArchetypeSource source = JsonArchetypeSource.Read(
                Path.Combine(_folder, "no-such-folder"), "ashfall");

            Assert.Empty(source.Ids);
            Assert.Empty(source.Problems);
        }

        // the filesystem's enumeration order is the filesystem's opinion, and a load order that
        // differs between machines is a bug nobody can reproduce
        [Fact]
        public void TheOrderIsTheSameEveryTime()
        {
            Write("zzz.json", Monster("zed"));
            Write("aaa.json", Monster("alpha"));
            Write("mmm.json", Monster("mid"));

            string[] first = Read().Ids.ToArray();
            string[] second = Read().Ids.ToArray();

            Assert.Equal(first, second);
        }

        [Fact]
        public void AskingForSomethingItDoesNotHaveSaysSo()
        {
            Write("ghoul.json", Monster("ghoul"));

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => Read().Create("ashfall.dragon"));
        }

        // ---- several rosters, one seam ----

        [Fact]
        public void TheEnginesRosterAndACampaignsStandSideBySide()
        {
            Write("ghoul.json", Monster("ghoul"));

            var rosters = new Rosters(new BuiltInArchetypes(), Read());

            Assert.True(rosters.Has(EngineIds.Rabble));
            Assert.True(rosters.Has("ashfall.ghoul"));
            Assert.Empty(rosters.Collisions);
            Assert.Equal(Tier.Rabble, rosters.Create(EngineIds.Rabble).Tier);
        }

        // TWO CAMPAIGNS CAN BOTH HAVE A GHOUL, which is the entire reason ids are scoped
        [Fact]
        public void AndTwoCampaignsCanBothHaveAGhoul()
        {
            Write("ghoul.json", Monster("ghoul"));

            var rosters = new Rosters(Read("ashfall"), Read("other"));

            Assert.Equal(2, rosters.Ids.Count);
            Assert.Empty(rosters.Collisions);
            Assert.True(rosters.Has("ashfall.ghoul"));
            Assert.True(rosters.Has("other.ghoul"));
        }

        // and if two ever did claim one id, that is a namespacing failure upstream and is said out
        // loud rather than resolved by whoever happened to load first
        [Fact]
        public void ACollisionIsNamedRatherThanResolvedQuietly()
        {
            Write("ghoul.json", Monster("ghoul"));

            var rosters = new Rosters(Read("ashfall"), Read("ashfall"));

            Assert.Contains("ashfall.ghoul", rosters.Collisions);
        }
    }
}

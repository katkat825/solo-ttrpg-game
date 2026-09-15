using System;
using System.IO;
using System.Linq;
using Content.Monsters;
using Core.Characters;
using Core.Dice;
using Xunit;

namespace Content.Tests
{
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
            try { Directory.Delete(_folder, recursive: true); } catch { }
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

        [Fact]
        public void AMissingFolderIsEmptyRatherThanAProblem()
        {
            JsonArchetypeSource source = JsonArchetypeSource.Read(
                Path.Combine(_folder, "no-such-folder"), "ashfall");

            Assert.Empty(source.Ids);
            Assert.Empty(source.Problems);
        }

        // sort load order so it does not vary by filesystem between machines
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

        [Fact]
        public void ACollisionIsNamedRatherThanResolvedQuietly()
        {
            Write("ghoul.json", Monster("ghoul"));

            var rosters = new Rosters(Read("ashfall"), Read("ashfall"));

            Assert.Contains("ashfall.ghoul", rosters.Collisions);
        }
    }
}

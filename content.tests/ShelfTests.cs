using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Minis;
using Content.Schema;
using Xunit;

namespace Content.Tests
{
    public sealed class ShelfTests : IDisposable
    {
        readonly string _root;

        public ShelfTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "shelf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }


        void Write(string pack, string relative, string text)
        {
            string path = Path.Combine(_root, pack, relative.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        static string Head(string id, string kind = null, string extra = "") => $@"{{
            ""id"": ""{id}"",
            {(kind == null ? "" : $@"""kind"": ""{kind}"",")}
            ""format"": {ContentFormat.Current},
            ""engine"": ""{Core.EngineVersion.Current}""
            {extra}
        }}";

        void Campaign(string id, string monster = "ghoul", string mini = null, string extra = "")
        {
            Write(id, ManifestReader.FileName,
                  Head(id, extra: $@", ""chapters"": [ {{ ""id"": ""one"",
                                        ""encounters"": [ ""yard"" ] }} ]{extra}"));

            Write(id, $"monsters/{monster}.json", $@"{{
                ""id"": ""{monster}"",
                ""tier"": ""rival"",
                ""vigor"": 10,
                ""defense"": 11,
                ""attributes"": {{ ""might"": ""d8"" }}
                {(mini == null ? "" : $@", ""mini"": ""{mini}""")}
            }}");

            Write(id, "maps/yard.map", "+-+-+-+-+\n|. . 1 .|\n+ + + + +\n|@ . . .|\n+-+-+-+-+\n");

            Write(id, "encounters/yard.json", $@"{{
                ""id"": ""yard"",
                ""map"": ""yard"",
                ""placements"": [ {{ ""slot"": 1, ""monster"": ""{monster}"" }} ]
            }}");
        }

        void MiniPack(string id, string mini = "oldbones", string variant = "rabble")
        {
            Write(id, ManifestReader.PackFileName, Head(id, kind: "minis"));

            Write(id, $"minis/{mini}.json", $@"{{
                ""id"": ""{mini}"",
                ""variant"": ""{variant}"",
                ""tint"": ""#D8CFB8"",
                ""height"": 0.082
            }}");
        }

        Shelf Read() => Shelf.Of(
            Directory.EnumerateDirectories(_root).OrderBy(f => f, StringComparer.Ordinal)
                     .Select(Package.Read));

        static string Why(Shelf shelf) =>
            string.Join("; ", shelf.Problems.Select(p => p.ToString()));


        [Fact]
        public void APackOfMinisIsAFolderAndACampaignCanDependOnIt()
        {
            MiniPack("grimdark");
            Campaign("ashfall", mini: "grimdark.oldbones",
                     extra: @", ""dependencies"": [ ""grimdark"" ]");

            Shelf shelf = Read();

            Assert.Empty(shelf.Problems);
            Assert.All(shelf.Entries, e => Assert.True(e.InPlay, e.ToString()));

            Mounted mounted = shelf.Mount("ashfall", "grimdark.oldbones");

            Assert.NotNull(mounted);
            Assert.Equal(SharedMinis.Rabble, mounted.Source);
            Assert.Equal(0.082f, mounted.Height);
            Assert.True(mounted.Tint.IsSomething);
        }

        [Fact]
        public void ACampaignWhoseDependencyIsNotInstalledIsOnTheShelfAndOutOfPlay()
        {
            Campaign("ashfall", extra: @", ""dependencies"": [ ""grimdark"" ]");

            Shelf shelf = Read();

            Shelf.Entry entry = shelf.Entries.Single();

            Assert.False(entry.InPlay);
            Assert.False(entry.Package.Failed);
            Assert.Equal(new[] { "grimdark" }, entry.Missing);
            Assert.Contains(shelf.Problems, p => p.What.Contains("grimdark") &&
                                                 p.What.Contains("not installed"));
        }

        [Fact]
        public void AWaitingCampaignDoesNotTakeTheRestOfTheShelfWithIt()
        {
            Campaign("ashfall", extra: @", ""dependencies"": [ ""nobody"" ]");
            Campaign("greyhollow", monster: "leech");

            Shelf shelf = Read();

            Assert.False(shelf.Of("ashfall").InPlay);
            Assert.True(shelf.Of("greyhollow").InPlay);
            Assert.Single(shelf.Loaded);
        }

        [Fact]
        public void APackThatSortsAfterTheOneNeedingItStillResolves()
        {
            MiniPack("zzz_minis");
            Campaign("aaa_campaign", extra: @", ""dependencies"": [ ""zzz_minis"" ]");

            Assert.True(Read().Of("aaa_campaign").InPlay);
        }


        [Fact]
        public void ABareMiniNameFindsThisPacksOwnBeforeTheSharedRosters()
        {
            MiniPack("grimdark", mini: "rabble", variant: "rival");

            Shelf shelf = Read();

            Assert.Equal(SharedMinis.Rival, shelf.Mount("grimdark", "rabble").Source);

            Assert.Equal(SharedMinis.Rabble, shelf.Mount("ashfall", "rabble").Source);
        }

        [Fact]
        public void AMonsterStandingOnAMiniNobodyShipsIsReportedAndTheFightIsStillPlayable()
        {
            Campaign("ashfall", mini: "grimdark.oldbones");

            Shelf shelf = Read();

            Assert.True(shelf.Of("ashfall").InPlay);
            Assert.Contains(shelf.Problems, p => p.Where == "mini" &&
                                                 p.What.Contains("placeholder box"));
        }

        [Fact]
        public void AMonsterStandingOnAMiniItsOwnPackShipsIsFine()
        {
            Write("ashfall", ManifestReader.PackFileName,
                  Head("ashfall", kind: "mixed",
                       extra: @", ""chapters"": [ { ""id"": ""one"", ""encounters"": [ ""yard"" ] } ]"));

            Campaign("ashfall", mini: "oldbones");
            File.Delete(Path.Combine(_root, "ashfall", ManifestReader.FileName));

            Write("ashfall", "minis/oldbones.json",
                  @"{ ""id"": ""oldbones"", ""variant"": ""rabble"", ""height"": 0.082 }");

            Shelf shelf = Read();

            Assert.Empty(shelf.Problems);
            Assert.Equal("ashfall.oldbones", shelf.Mount("ashfall", "oldbones").Id);
        }


        [Fact]
        public void APackJsonMustSayWhatKindOfPackItIs()
        {
            Write("grimdark", ManifestReader.PackFileName, Head("grimdark"));

            Package package = Package.Read(Path.Combine(_root, "grimdark"));

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.Where == "kind");
        }

        [Fact]
        public void ACampaignJsonWithNoKindIsStillACampaign()
        {
            Campaign("ashfall");

            Package package = Package.Read(Path.Combine(_root, "ashfall"));

            Assert.False(package.Failed, string.Join("; ", package.Problems.Select(p => p.ToString())));
            Assert.Equal(PackKind.Campaign, package.Manifest.Kind);
            Assert.True(package.Manifest.IsPlayable);
        }

        [Fact]
        public void AMiniPackIsNotAskedForChapters()
        {
            MiniPack("grimdark");

            Package package = Package.Read(Path.Combine(_root, "grimdark"));

            Assert.False(package.Failed, string.Join("; ", package.Problems.Select(p => p.ToString())));
            Assert.Equal(PackKind.Minis, package.Manifest.Kind);
            Assert.False(package.Manifest.IsPlayable);
            Assert.Single(package.Minis.Ids);
        }

        [Fact]
        public void AMiniPackThatWroteChaptersAnywayIsToldWhatMixedIsFor()
        {
            Write("grimdark", ManifestReader.PackFileName,
                  Head("grimdark", kind: "minis",
                       extra: @", ""chapters"": [ { ""id"": ""one"", ""encounters"": [ ""x"" ] } ]"));

            Package package = Package.Read(Path.Combine(_root, "grimdark"));

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.What.Contains("mixed"));
        }

        [Fact]
        public void AFolderWithBothManifestNamesIsAForkSomebodyHalfFinished()
        {
            Campaign("ashfall");
            Write("ashfall", ManifestReader.PackFileName, Head("ashfall", kind: "campaign"));

            Package package = Package.Read(Path.Combine(_root, "ashfall"));

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.What.Contains("under two names"));
        }

        [Fact]
        public void AClassPackLoadsSinceK0()
        {
            Write("heroes_plus", ManifestReader.PackFileName, Head("heroes_plus", kind: "classes"));

            Package package = Package.Read(Path.Combine(_root, "heroes_plus"));

            Assert.False(package.Failed);
            Assert.Equal(PackKind.Classes, package.Manifest.Kind);

            Assert.False(package.Manifest.IsPlayable);
        }

        [Fact]
        public void AClassPackServesItsClassesThroughTheSeam()
        {
            Write("heroes_plus", ManifestReader.PackFileName, Head("heroes_plus", kind: "classes"));
            Write("heroes_plus", "classes/warden.json", @"{
                ""id"": ""warden"",
                ""vigor"": 20,
                ""defense"": 11,
                ""attributes"": { ""might"": ""d8"" },
                ""wields"": ""axe""
            }");

            Package package = Package.Read(Path.Combine(_root, "heroes_plus"));

            Assert.Empty(package.Problems);

            Assert.True(package.Classes.Has("heroes_plus.warden"));

            Assert.Equal(Core.Dice.Die.D6, package.Classes.Create("heroes_plus.warden").Weapon);
        }

        [Fact]
        public void AClassStartingYouWithGearNobodyShipsIsNamed()
        {
            Write("heroes_plus", ManifestReader.PackFileName, Head("heroes_plus", kind: "classes"));
            Write("heroes_plus", "classes/warden.json", @"{
                ""id"": ""warden"",
                ""vigor"": 20,
                ""defense"": 11,
                ""attributes"": { ""might"": ""d8"" },
                ""wields"": ""moonblade""
            }");

            Package package = Package.Read(Path.Combine(_root, "heroes_plus"));

            Assert.Contains(package.Problems,
                            p => p.What.Contains("a class has to start you with something that exists"));
        }

        [Fact]
        public void APackCannotDependOnItself()
        {
            Campaign("ashfall", extra: @", ""dependencies"": [ ""ashfall"" ]");

            Package package = Package.Read(Path.Combine(_root, "ashfall"));

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.What.Contains("cannot depend on itself"));
        }


        [Fact]
        public void AVariantReachingIntoAPackNobodyInstalledIsNamedRatherThanSilent()
        {
            MiniPack("grimdark", mini: "borrowed", variant: "somebody_else.thing");

            Shelf shelf = Read();

            Assert.Contains(shelf.Problems, p => p.What.Contains("somebody_else.thing"));
            Assert.Null(shelf.Mount("grimdark", "borrowed"));
        }

        [Fact]
        public void TwoPacksVaryingEachOtherAreRefusedRatherThanWalkedForever()
        {
            MiniPack("one", mini: "a", variant: "two.b");
            MiniPack("two", mini: "b", variant: "one.a");

            Shelf shelf = Read();

            Assert.Contains(shelf.Problems, p => p.What.Contains("variants of each other"));
            Assert.Null(shelf.Mount("one", "a"));
        }
    }
}

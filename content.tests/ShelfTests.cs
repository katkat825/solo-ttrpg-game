using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Minis;
using Content.Schema;
using Xunit;

namespace Content.Tests
{
    // EVERY PACK ON THIS MACHINE, SIDE BY SIDE (MINIS_AND_ART.md A4, MODDING.md section 2).
    //
    // `PackageTests` covers what one folder can answer about itself. This covers the two questions
    // it deliberately cannot: is the pack this one depends on installed, and does this mini id
    // resolve across every root. Both are about the SHELF, and both are new with this phase.
    //
    // AND IT IS WHERE THE PHASE'S REAL DELIVERABLE IS PINNED - `MODDING.md` section 6's pack test:
    // "a raw model the game has never seen, playing from a folder, with an empty codebase diff."
    // `APackOfMinisIsAFolderAndACampaignCanDependOnIt` below is that test, headless: two folders
    // written from a string, one standing the other's figures, and nothing compiled to do it.
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
            try { Directory.Delete(_root, recursive: true); } catch { /* a temp folder */ }
        }

        // ---- writing packs ----

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

        // a campaign: a manifest with a chapter, one map, one encounter, one monster
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

        // a mini pack: a manifest that says so, and one variant of a shipped figure
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

        // ---- A4: a pack is a folder, and a campaign is a pack ----

        [Fact]
        public void APackOfMinisIsAFolderAndACampaignCanDependOnIt()
        {
            MiniPack("grimdark");
            Campaign("ashfall", mini: "grimdark.oldbones",
                     extra: @", ""dependencies"": [ ""grimdark"" ]");

            Shelf shelf = Read();

            Assert.Empty(shelf.Problems);
            Assert.All(shelf.Entries, e => Assert.True(e.InPlay, e.ToString()));

            // AND THE FIGURE RESOLVES ACROSS THE TWO FOLDERS, which is the whole feature: one
            // pack's monster standing on another pack's mini, with nothing compiled to do it
            Mounted mounted = shelf.Mount("ashfall", "grimdark.oldbones");

            Assert.NotNull(mounted);
            Assert.Equal(SharedMinis.Rabble, mounted.Source);
            Assert.Equal(0.082f, mounted.Height);
            Assert.True(mounted.Tint.IsSomething);
        }

        // "reports a missing dependency by name rather than half-loading the campaign" (A4)
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

        // ONE FOLDER'S PROBLEM IS ONE FOLDER'S. The isolation boundary, checked at the level A4
        // added: a campaign waiting on a subscription does not take its neighbour with it
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

        // A DEPENDENCY DOES NOT CARE WHAT ORDER THE ROOTS WERE WALKED IN, which is why the shelf
        // is assembled whole before anything is put in play. `zzz` sorts after `aaa`
        [Fact]
        public void APackThatSortsAfterTheOneNeedingItStillResolves()
        {
            MiniPack("zzz_minis");
            Campaign("aaa_campaign", extra: @", ""dependencies"": [ ""zzz_minis"" ]");

            Assert.True(Read().Of("aaa_campaign").InPlay);
        }

        // ---- and the mini ids, resolved across every root ----

        [Fact]
        public void ABareMiniNameFindsThisPacksOwnBeforeTheSharedRosters()
        {
            MiniPack("grimdark", mini: "rabble", variant: "rival");

            Shelf shelf = Read();

            // `rabble` inside grimdark means grimdark's, which is a variant of the shared `rival`
            Assert.Equal(SharedMinis.Rival, shelf.Mount("grimdark", "rabble").Source);

            // and from anywhere else it is still the shared one
            Assert.Equal(SharedMinis.Rabble, shelf.Mount("ashfall", "rabble").Source);
        }

        [Fact]
        public void AMonsterStandingOnAMiniNobodyShipsIsReportedAndTheFightIsStillPlayable()
        {
            Campaign("ashfall", mini: "grimdark.oldbones");

            Shelf shelf = Read();

            // NOT FATAL. "the piece is where it should be, the fight is playable, and the shelf
            // tile says which mini failed" - so the campaign loads and the problem is named
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

        // ---- the manifest's two names ----

        [Fact]
        public void APackJsonMustSayWhatKindOfPackItIs()
        {
            Write("grimdark", ManifestReader.PackFileName, Head("grimdark"));

            Package package = Package.Read(Path.Combine(_root, "grimdark"));

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.Where == "kind");
        }

        // EVERY CAMPAIGN ALREADY WRITTEN KEEPS WORKING. That is the whole compatibility story
        [Fact]
        public void ACampaignJsonWithNoKindIsStillACampaign()
        {
            Campaign("ashfall");

            Package package = Package.Read(Path.Combine(_root, "ashfall"));

            Assert.False(package.Failed, string.Join("; ", package.Problems.Select(p => p.ToString())));
            Assert.Equal(PackKind.Campaign, package.Manifest.Kind);
            Assert.True(package.Manifest.IsPlayable);
        }

        // "a mini pack has no chapters and is not missing any" - which absences are worth a
        // sentence is exactly what a kind decides
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
        public void AClassPackIsSpeccedAndNotBuiltAndSaysWhichPhaseItIs()
        {
            Write("heroes_plus", ManifestReader.PackFileName, Head("heroes_plus", kind: "classes"));

            Package package = Package.Read(Path.Combine(_root, "heroes_plus"));

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.What.Contains("CLASSES_AND_KITS"));
        }

        [Fact]
        public void APackCannotDependOnItself()
        {
            Campaign("ashfall", extra: @", ""dependencies"": [ ""ashfall"" ]");

            Package package = Package.Read(Path.Combine(_root, "ashfall"));

            Assert.True(package.Failed);
            Assert.Contains(package.Problems, p => p.What.Contains("cannot depend on itself"));
        }

        // ---- a variant of a mini in a pack that is not there ----

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

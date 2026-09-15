using System.Collections.Generic;
using Content.Minis;
using Xunit;

namespace Content.Tests
{
    public sealed class MiniRegistryTests
    {
        const string Pack = "grimdark";

        static MiniManifest Mini(string id, string variant = null, string model = null,
                                 Fit? fit = null, float? height = null, float? foot = null,
                                 Tint tint = default,
                                 Dictionary<Motion, string> clips = null,
                                 Dictionary<Motion, string> foley = null) =>
            new MiniManifest(id, variant, model, fit, height, foot, tint, clips, foley);

        static MiniRegistry With(params MiniManifest[] minis)
        {
            var registry = new MiniRegistry();

            registry.Add(MiniCatalogue.Of(minis), "/packs/" + Pack);

            return registry;
        }

        static Tint Bone
        {
            get
            {
                Tint.TryParse("#D8CFB8", out Tint tint);
                return tint;
            }
        }


        [Fact]
        public void TheSharedRosterIsInAnEmptyRegistry()
        {
            var registry = new MiniRegistry();

            Mounted mounted = registry.Mount(SharedMinis.Rabble);

            Assert.NotNull(mounted);
            Assert.True(mounted.IsShipped);
            Assert.Equal(SharedMinis.Rabble, mounted.Source);
        }


        [Fact]
        public void AVariantResolvesToTheFigureItIsAVariantOf()
        {
            MiniRegistry registry = With(Mini("grimdark.oldbones", variant: SharedMinis.Rabble,
                                              height: 0.082f, tint: Bone));

            Mounted mounted = registry.Mount("grimdark.oldbones");

            Assert.NotNull(mounted);
            Assert.True(mounted.IsShipped);
            Assert.Equal(SharedMinis.Rabble, mounted.Source);
            Assert.Equal(0.082f, mounted.Height);
            Assert.True(mounted.Tint.IsSomething);
        }

        // fit.cell is the enum's zero, so an inherited size must not silently become square-fitted
        [Fact]
        public void AVariantInheritsEverythingItDidNotSay()
        {
            MiniRegistry registry = With(Mini("grimdark.pale", variant: SharedMinis.Rabble, tint: Bone));

            Mounted mounted = registry.Mount("grimdark.pale");

            Assert.Equal(Fit.Height, mounted.Fit);
            Assert.Equal(0.05f, mounted.Height);
        }

        [Fact]
        public void TheMiniThatWasAskedForGetsTheLastWord()
        {
            MiniRegistry registry = With(
                Mini("grimdark.big", variant: SharedMinis.Rabble, height: 0.09f),
                Mini("grimdark.bigger", variant: "grimdark.big", height: 0.11f));

            Assert.Equal(0.11f, registry.Mount("grimdark.bigger").Height);
        }

        [Fact]
        public void AVariantOfAVariantReachesTheModelAtTheEnd()
        {
            MiniRegistry registry = With(
                Mini("grimdark.skeleton", model: "models/skeleton.glb"),
                Mini("grimdark.pale", variant: "grimdark.skeleton", tint: Bone),
                Mini("grimdark.paler", variant: "grimdark.pale", height: 0.08f));

            Mounted mounted = registry.Mount("grimdark.paler");

            Assert.False(mounted.IsShipped);
            Assert.Equal("grimdark.skeleton", mounted.Source);
            Assert.Equal("models/skeleton.glb", mounted.Model);
            Assert.True(mounted.Tint.IsSomething);
            Assert.Equal(0.08f, mounted.Height);
        }


        [Fact]
        public void AVariantOverridesOneClipAndInheritsTheRest()
        {
            MiniRegistry registry = With(
                Mini("grimdark.skeleton", model: "a.glb", clips: new Dictionary<Motion, string>
                {
                    [Motion.Move] = "Walk",
                    [Motion.Topple] = "Death_A",
                }),
                Mini("grimdark.crawler", variant: "grimdark.skeleton",
                     clips: new Dictionary<Motion, string> { [Motion.Move] = "Crawl" }));

            Mounted mounted = registry.Mount("grimdark.crawler");

            Assert.Equal("Crawl", mounted.ClipFor(Motion.Move));
            Assert.Equal("Death_A", mounted.ClipFor(Motion.Topple));
        }

        [Fact]
        public void AFoleyFolderIsMadeAbsoluteAgainstThePackThatNamedIt()
        {
            MiniRegistry registry = With(
                Mini("grimdark.skeleton", model: "a.glb",
                     foley: new Dictionary<Motion, string> { [Motion.Placed] = "audio/bones" }));

            Assert.Contains("grimdark", registry.Mount("grimdark.skeleton").FoleyFor(Motion.Placed));
            Assert.EndsWith("bones", registry.Mount("grimdark.skeleton").FoleyFor(Motion.Placed));
        }


        [Fact]
        public void AVariantOfSomethingNobodyShipsIsNullAndSaysWhichPackIsMissing()
        {
            MiniRegistry registry = With(Mini("grimdark.borrowed", variant: "somebody_else.thing"));

            Mounted mounted = registry.Mount("grimdark.borrowed", out string why);

            Assert.Null(mounted);
            Assert.Contains("somebody_else.thing", why);
            Assert.Contains("dependencies", why);
        }

        [Fact]
        public void TwoMinisThatAreVariantsOfEachOtherAreRefusedRatherThanWalkedForever()
        {
            MiniRegistry registry = With(
                Mini("grimdark.a", variant: "grimdark.b"),
                Mini("grimdark.b", variant: "grimdark.a"));

            Mounted mounted = registry.Mount("grimdark.a", out string why);

            Assert.Null(mounted);
            Assert.Contains("variants of each other", why);
            Assert.Contains("grimdark.a", why);
            Assert.Contains("grimdark.b", why);
        }

        [Fact]
        public void AMiniThatIsAVariantOfItselfIsTheSameFailure()
        {
            MiniRegistry registry = With(Mini("grimdark.self", variant: "grimdark.self"));

            Assert.Null(registry.Mount("grimdark.self"));
        }

        [Fact]
        public void AChainDeeperThanTheBoundIsRefusedByName()
        {
            var minis = new List<MiniManifest> { Mini("grimdark.m00", model: "a.glb") };

            for (int at = 1; at <= MiniRegistry.LongestChain + 2; at++)
                minis.Add(Mini($"grimdark.m{at:00}", variant: $"grimdark.m{at - 1:00}"));

            MiniRegistry registry = With(minis.ToArray());

            Assert.Null(registry.Mount($"grimdark.m{MiniRegistry.LongestChain + 2:00}", out string why));
            Assert.Contains("deep", why);
        }

        [Fact]
        public void AnIdNothingShipsIsNullRatherThanAnException()
        {
            Assert.Null(new MiniRegistry().Mount("nobody.nothing", out string why));
            Assert.Contains("may not be installed", why);
        }

        [Fact]
        public void AnIdClaimedTwiceIsReportedAsANamespacingFailure()
        {
            var registry = new MiniRegistry();

            registry.Add(MiniCatalogue.Of(new[] { Mini(SharedMinis.Rabble, model: "a.glb") }), "/packs/x");

            Assert.Contains(registry.Problems, p => p.What.Contains("not namespaced"));
        }
    }
}

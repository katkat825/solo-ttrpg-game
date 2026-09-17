using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Places;
using Content.Schema;
using Xunit;

namespace Content.Tests
{
    // THE CAMPAIGNS THAT SHIP, READ BY THE SAME VALIDATOR THE GAME RUNS.
    //
    // check-campaign.ps1 does this against every folder on the machine and prints what it read
    // back; this is the same question asked from the test suite, so a campaign folder cannot be
    // broken by a refactor without something going red before anybody opens Godot.
    //
    // ashfall and greyhollow are the regression check the World layer promised: if the reframe
    // made a plain linear dungeon harder to express, the reframe was wrong (section 12). Neither
    // gained a field to become places. saltmarch is the third campaign test - a world rather than
    // a corridor - and it is here for the opposite reason: to prove the new half is authorable at
    // all, with no C# written to make it exist.
    public sealed class ShippedCampaignTests
    {
        static string Root(string folder = "campaigns")
        {
            var here = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (here != null && !Directory.Exists(Path.Combine(here.FullName, folder)))
                here = here.Parent;

            return here == null ? null : Path.Combine(here.FullName, folder);
        }

        static Package Read(string campaign) => Package.Read(Path.Combine(Root(), campaign));

        static string Said(Package package) =>
            string.Join("; ", package.Problems.Select(p => p.ToString()));

        [Theory]
        [InlineData("ashfall")]
        [InlineData("greyhollow")]
        [InlineData("saltmarch")]
        public void ItLoadsWithNothingWrongWithIt(string campaign)
        {
            Package package = Read(campaign);

            Assert.False(package.Failed, Said(package));
            Assert.True(package.Clean, Said(package));
        }

        [Fact]
        public void ALinearDungeonIsStillThreeFilesWithFoesOnThem()
        {
            Package package = Read("greyhollow");

            Assert.Equal(3, package.Places.Count);
            Assert.All(package.Places.All, place => Assert.True(place.IsAFight));

            // no entities, no exits, no quests, no roads - the degenerate case costs nothing
            Assert.Equal(0, package.Entities.Count);
            Assert.Equal(0, package.Quests.Count);
            Assert.Equal(0, package.Roads.Count);
            Assert.All(package.Places.All, place => Assert.Empty(place.Exits));
        }

        [Fact]
        public void AWorldIsPlacesWithPeopleOnThemAndWaysBetweenThem()
        {
            Package package = Read("saltmarch");

            Place quay = package.Places.Of("the_quay");

            Assert.NotNull(quay);
            Assert.False(quay.IsAFight);
            Assert.Equal(3, quay.Standings.Count);
            Assert.Single(quay.Exits);

            Assert.Equal(4, package.Entities.Count);
            Assert.Equal(1, package.Quests.Count);
            Assert.Equal(1, package.Roads.Count);
        }

        // A BROKEN TEMPLATE IS A BROKEN FIRST HOUR. Nothing loads templates/ at runtime - that
        // is the point of the folder - so nothing was holding them to the schema they teach, and
        // the World layer changed that schema.
        [Theory]
        [InlineData("my_campaign")]
        [InlineData("my_classes")]
        [InlineData("my_minis")]
        public void TheTemplateAnAuthorCopiesIsItselfValid(string template)
        {
            Package package = Package.Read(Path.Combine(Root("templates"), template));

            Assert.False(package.Failed, Said(package));
            Assert.True(package.Clean, Said(package));
        }

        [Fact]
        public void TheCampaignTemplateShowsBothKindsOfPlace()
        {
            Package package = Package.Read(Path.Combine(Root("templates"), "my_campaign"));

            Assert.Contains(package.Places.All, p => p.IsAFight);
            Assert.Contains(package.Places.All, p => !p.IsAFight && p.Exits.Count > 0);

            Assert.True(package.Entities.Count > 0);
            Assert.True(package.Quests.Count > 0);
            Assert.True(package.Roads.Count > 0);
        }

        [Fact]
        public void TheValidatorWarnsAboutTheAttackableQuestGiverWithoutRefusingHim()
        {
            Package package = Read("saltmarch");

            ContentProblem caution = Assert.Single(package.Cautions);

            Assert.Contains("norrel", caution.What);
            Assert.Contains("attackable", caution.What);
            Assert.True(package.Clean);
        }
    }
}

using System;
using System.Linq;
using Content.World;
using Xunit;

namespace Content.Tests
{
    // The keystone, on its own (PLACES_AND_PERSISTENCE.md section 3). Everything else in the World
    // layer talks to this, so it is worth knowing exactly what it does before any of them do.
    public sealed class FactTests
    {
        static Facts Some() => Facts.For("ashfall");

        [Fact]
        public void AFactIsDottedWhereAContentIdIsNot()
        {
            Assert.True(FactName.IsLocal("bob"));
            Assert.True(FactName.IsLocal("bob.dead"));
            Assert.True(FactName.IsLocal("chest.ash_yard.looted"));

            Assert.False(FactName.IsLocal("Bob.dead"));
            Assert.False(FactName.IsLocal("bob..dead"));
            Assert.False(FactName.IsLocal("bob dead"));
            Assert.False(FactName.IsLocal(""));
        }

        [Fact]
        public void AFactWithMorePartsThanASentenceIsRefused()
        {
            Assert.False(FactName.IsLocal("a.b.c.d.e"));
            Assert.Contains("at most", FactName.Explain("a.b.c.d.e"));
        }

        [Fact]
        public void TheStoreKnowsItsCampaignAndScopesWithIt()
        {
            Facts facts = Some();

            facts.Set("bob.dead");

            Assert.Equal("ashfall", facts.Campaign);
            Assert.Equal("ashfall.bob.dead", Assert.Single(facts.Scoped));
            Assert.Equal("bob.dead", Assert.Single(facts.All));
        }

        [Fact]
        public void SettingSaysWhetherItChangedAnything()
        {
            Facts facts = Some();

            Assert.True(facts.Set("bob.dead"));
            Assert.False(facts.Set("bob.dead"));
            Assert.True(facts.Clear("bob.dead"));
            Assert.False(facts.Clear("bob.dead"));
        }

        [Fact]
        public void AFactNobodyCouldHaveMeantIsRefusedRatherThanStoredSilently()
        {
            Assert.Throws<ArgumentException>(() => Some().Set("Bob Is Dead"));
        }

        [Fact]
        public void TheEngineDerivesItsOwnAspectsRatherThanTakingThemFromAnAuthor()
        {
            Assert.Equal("bob.dead", FactName.Dead("bob"));
            Assert.Equal("chest.looted", FactName.Looted("chest"));
            Assert.Equal("ash_yard.cleared", FactName.Cleared("ash_yard"));
            Assert.Equal("ash_yard.visited", FactName.Visited("ash_yard"));

            Assert.Equal("bob", FactName.SubjectOf("bob.dead", FactName.DeadAspect));
            Assert.Equal("", FactName.SubjectOf("bob.spoken", FactName.DeadAspect));
        }

        [Fact]
        public void AbsorbingASaveKeepsWhatItCanSpellAndDropsWhatItCannot()
        {
            Facts facts = Some();

            facts.Absorb(new[] { "bob.dead", "NOT A FACT", null, "chest.looted" });

            Assert.Equal(new[] { "bob.dead", "chest.looted" }, facts.All);
        }

        [Fact]
        public void ARequirementWithNothingInItIsAlwaysMet()
        {
            Assert.True(Requirement.Always.IsAlways);
            Assert.True(Requirement.Always.Met(Some()));
            Assert.True(Requirement.Always.Met(null));
        }

        [Fact]
        public void WhenNeedsAllOfThemAndUnlessNeedsNoneOfThem()
        {
            var needs = new Requirement(new[] { "bob.dead" }, new[] { "cemetery.sealed" });
            Facts facts = Some();

            Assert.False(needs.Met(facts));

            facts.Set("bob.dead");
            Assert.True(needs.Met(facts));

            facts.Set("cemetery.sealed");
            Assert.False(needs.Met(facts));
        }

        [Fact]
        public void ARequirementNamesEveryFactItReadsSoTheValidatorCanFollowIt()
        {
            var needs = new Requirement(new[] { "bob.dead" }, new[] { "cemetery.sealed" });

            Assert.Equal(new[] { "bob.dead", "cemetery.sealed" }, needs.Facts.ToArray());
        }
    }
}

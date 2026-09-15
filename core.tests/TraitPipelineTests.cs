using System;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Xunit;

namespace Core.Tests
{
    public class TraitPipelineTests
    {
        static readonly ModifierSource Ring = ModifierSource.Gear("ring_of_might");
        static readonly ModifierSource Rage = ModifierSource.Effect("rage");

        static TraitPipeline Pipeline(Die might = Die.D8)
        {
            var p = new TraitPipeline();
            p.SetBase(Attr.Might, might);
            return p;
        }


        [Fact]
        public void StepBy_MovesAndReportsWhatItDelivered()
        {
            Assert.Equal(Die.D4, Die.D8.StepBy(-2, out int down));
            Assert.Equal(-2, down);

            Assert.Equal(Die.D12, Die.D8.StepBy(2, out int up));
            Assert.Equal(2, up);

            Assert.Equal(Die.D8, Die.D8.StepBy(0, out int still));
            Assert.Equal(0, still);
        }

        [Fact]
        public void StepBy_ClampsAtBothEnds_AndSaysHowFarItGot()
        {
            Assert.Equal(Die.D4, Die.D6.StepBy(-3, out int floored));
            Assert.Equal(-1, floored);

            Assert.Equal(Die.D12, Die.D10.StepBy(4, out int capped));
            Assert.Equal(1, capped);
        }

        [Fact]
        public void StepBy_LeavesNoneAlone_BecauseItIsNotOnTheLadder()
        {
            Assert.Equal(Die.None, Die.None.StepBy(-3, out int delivered));
            Assert.Equal(0, delivered);
        }


        [Fact]
        public void TwoDownwardModifiers_ComposeToTwoSteps()
        {
            var p = Pipeline(Die.D12);
            p.Add(new TraitModifier(Ring, Attr.Might, -1));
            p.Add(new TraitModifier(Rage, Attr.Might, -1));

            Assert.Equal(Die.D8, p.Current(Attr.Might));
            Assert.Equal(0, p.Refused(Attr.Might));
        }

        [Fact]
        public void OppositeModifiers_GiveTheSameDieWhicheverArrivedFirst()
        {
            var down = new TraitModifier(Ring, Attr.Might, -2);
            var up = new TraitModifier(Rage, Attr.Might, 1);

            var a = Pipeline();
            a.Add(down);
            a.Add(up);

            var b = Pipeline();
            b.Add(up);
            b.Add(down);

            Assert.Equal(Die.D6, a.Current(Attr.Might));
            Assert.Equal(a.Current(Attr.Might), b.Current(Attr.Might));
        }

        // a replaying pipeline gets this wrong: -2 floors a d6, then +1 climbs back, so order would matter
        [Fact]
        public void AStepDownThroughTheFloor_DoesNotBankAStepUp()
        {
            var a = Pipeline(Die.D6);
            a.Add(new TraitModifier(Ring, Attr.Might, -2));
            a.Add(new TraitModifier(Rage, Attr.Might, 1));

            var b = Pipeline(Die.D6);
            b.Add(new TraitModifier(Rage, Attr.Might, 1));
            b.Add(new TraitModifier(Ring, Attr.Might, -2));

            Assert.Equal(Die.D4, a.Current(Attr.Might));
            Assert.Equal(Die.D4, b.Current(Attr.Might));
            Assert.Equal(0, a.Refused(Attr.Might));
        }


        [Fact]
        public void ThreeStepsDownFromD6_IsAD4WithTwoRefused()
        {
            var p = Pipeline(Die.D6);
            p.Add(new TraitModifier(Ring, Attr.Might, -3));

            Assert.Equal(Die.D4, p.Current(Attr.Might));
            Assert.Equal(-2, p.Refused(Attr.Might));
        }

        [Fact]
        public void TwoStacksAndThreeStacks_LandOnTheSameDie_ButAreNotTheSameState()
        {
            var two = Pipeline(Die.D6);
            two.Add(new TraitModifier(Ring, Attr.Might, -2));

            var three = Pipeline(Die.D6);
            three.Add(new TraitModifier(Ring, Attr.Might, -3));

            Assert.Equal(two.Current(Attr.Might), three.Current(Attr.Might));
            Assert.NotEqual(two.Refused(Attr.Might), three.Refused(Attr.Might));
        }

        [Fact]
        public void PastTheCeiling_IsReportedTheSameWay()
        {
            var p = Pipeline(Die.D10);
            p.Add(new TraitModifier(Rage, Attr.Might, 3));

            Assert.Equal(Die.D12, p.Current(Attr.Might));
            Assert.Equal(2, p.Refused(Attr.Might));
        }

        [Fact]
        public void RemovingTheOverflow_ClearsTheRefusedCount()
        {
            var p = Pipeline(Die.D6);
            p.Add(new TraitModifier(Ring, Attr.Might, -3));
            p.RemoveAllFrom(Ring);

            Assert.Equal(Die.D6, p.Current(Attr.Might));
            Assert.Equal(0, p.Refused(Attr.Might));
        }


        [Fact]
        public void OneSourceComesOff_AndTheOtherStays()
        {
            var p = Pipeline(Die.D12);
            p.Add(new TraitModifier(Ring, Attr.Might, -1));
            p.Add(new TraitModifier(Rage, Attr.Might, -1));

            Assert.Equal(1, p.RemoveAllFrom(Ring));

            Assert.Equal(Die.D10, p.Current(Attr.Might));
            Assert.False(p.Has(Ring));
            Assert.True(p.Has(Rage));
        }

        [Fact]
        public void RemovingASource_TakesEveryAttributeItWasMoving()
        {
            var p = new TraitPipeline();
            p.SetBase(Attr.Might, Die.D8);
            p.SetBase(Attr.Grace, Die.D8);
            p.Add(new TraitModifier(Ring, Attr.Might, -1));
            p.Add(new TraitModifier(Ring, Attr.Grace, -1));

            Assert.Equal(2, p.RemoveAllFrom(Ring));

            Assert.Equal(Die.D8, p.Current(Attr.Might));
            Assert.Equal(Die.D8, p.Current(Attr.Grace));
            Assert.Empty(p.Modifiers);
        }

        [Fact]
        public void RemovingASourceThatIsNotThere_ChangesNothing()
        {
            var p = Pipeline();
            p.Add(new TraitModifier(Rage, Attr.Might, -1));

            Assert.Equal(0, p.RemoveAllFrom(Ring));
            Assert.Equal(Die.D6, p.Current(Attr.Might));
        }

        [Fact]
        public void SourcesOfDifferentKinds_AreDifferentSources()
        {
            Assert.NotEqual(ModifierSource.Gear("rage"), ModifierSource.Effect("rage"));
            Assert.Equal(ModifierSource.Gear("ring_of_might"), ModifierSource.Gear("ring_of_might"));
        }

        [Fact]
        public void AConditionIsItsOwnSource()
        {
            Assert.Equal(ModifierSource.FromCondition(Condition.Winded),
                         ModifierSource.FromCondition(Condition.Winded));

            Assert.NotEqual(ModifierSource.FromCondition(Condition.Winded),
                            ModifierSource.FromCondition(Condition.Reeling));
        }

        [Fact]
        public void ASourceWithNoId_IsRefused_BecauseNothingCouldRemoveIt()
        {
            Assert.Throws<ArgumentException>(() => ModifierSource.Gear(""));
            Assert.Throws<ArgumentException>(() => ModifierSource.Effect(null));
        }


        [Fact]
        public void SettingTheBaseLater_KeepsTheModifiersOnIt()
        {
            var p = Pipeline(Die.D8);
            p.Add(new TraitModifier(Ring, Attr.Might, -1));
            p.SetBase(Attr.Might, Die.D12);

            Assert.Equal(Die.D12, p.Base(Attr.Might));
            Assert.Equal(Die.D10, p.Current(Attr.Might));
        }

        [Fact]
        public void AnAttributeWithNoDie_CannotBeMovedOrFloored()
        {
            var p = new TraitPipeline();
            p.Add(new TraitModifier(Ring, Attr.Grace, -3));

            Assert.Equal(Die.None, p.Current(Attr.Grace));
            Assert.Equal(0, p.Refused(Attr.Grace));
        }

        [Fact]
        public void ModifiersKeepTheirInsertionOrder_EvenThoughTheArithmeticDoesNot()
        {
            var p = Pipeline();
            p.Add(new TraitModifier(Rage, Attr.Might, -1));
            p.Add(new TraitModifier(Ring, Attr.Might, -1));

            Assert.Equal(new[] { Rage, Ring }, p.Modifiers.Select(m => m.Source));
        }
    }
}

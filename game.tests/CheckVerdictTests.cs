using Game.Diagnostics;
using Xunit;

namespace Game.Tests
{
    // the contract check-fairness.ps1, check-locale.ps1 and anything wiring them into CI read:
    // what a check says and what it exits with
    //
    // it was hand-rolled in DiceFairness and again in LocaleAudit until F5, with nothing holding
    // the two copies together. these are the cases that would have caught them drifting, and the
    // ones the next three checks (a board sweep, a combat replay, a campaign-load audit) inherit
    // for free by deriving from HeadlessCheck
    public class CheckVerdictTests
    {
        [Fact]
        public void Passing_IsZero_AndFailing_IsOne()
        {
            // a check that exits 0 on failure is worse than no check at all
            Assert.Equal(0, CheckVerdict.ExitCode(true));
            Assert.Equal(1, CheckVerdict.ExitCode(false));

            Assert.Equal(CheckVerdict.Pass, CheckVerdict.ExitCode(true));
            Assert.Equal(CheckVerdict.Fail, CheckVerdict.ExitCode(false));
        }

        [Fact]
        public void APassIsQuiet_AndAFailureShouts()
        {
            Assert.Equal("fairness check passed", CheckVerdict.Line("fairness", true));
            Assert.Equal("FAIRNESS CHECK FAILED", CheckVerdict.Line("fairness", false));
        }

        // the exact lines both checks printed before F5, so the refactor is provably silent
        [Fact]
        public void TheWordingIsWhatTheTwoChecksPrintedBefore()
        {
            Assert.Equal("locale check passed", CheckVerdict.Line("locale", true));

            Assert.Equal("LOCALE CHECK FAILED with 2 problems",
                         CheckVerdict.Line("locale", false, CheckVerdict.Counted(2)));
        }

        [Fact]
        public void DetailIsAppendedToAFailureOnly()
        {
            // "passed with 0 problems" is noise
            Assert.Equal("locale check passed", CheckVerdict.Line("locale", true, "with 0 problems"));

            Assert.Equal("LOCALE CHECK FAILED with 0 problems",
                         CheckVerdict.Line("locale", false, "with 0 problems"));
        }

        [Fact]
        public void NoDetail_LeavesNoTrailingSpace()
        {
            Assert.Equal("LOCALE CHECK FAILED", CheckVerdict.Line("locale", false));
            Assert.Equal("LOCALE CHECK FAILED", CheckVerdict.Line("locale", false, ""));
            Assert.Equal("LOCALE CHECK FAILED", CheckVerdict.Line("locale", false, null));
        }

        [Theory]
        [InlineData(0, "with 0 problems")]
        [InlineData(1, "with 1 problem")]
        [InlineData(2, "with 2 problems")]
        [InlineData(53, "with 53 problems")]
        public void OneProblemIsSingular_AndEverythingElseIsNot(int problems, string expected)
        {
            Assert.Equal(expected, CheckVerdict.Counted(problems));
        }
    }
}

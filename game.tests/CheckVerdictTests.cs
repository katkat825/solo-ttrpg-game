using Game.Diagnostics;
using Xunit;

namespace Game.Tests
{
    public class CheckVerdictTests
    {
        [Fact]
        public void Passing_IsZero_AndFailing_IsOne()
        {
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

namespace Game.Diagnostics
{
    // how a headless check says what it found, and what it exits with - as plain strings and
    // ints, with no Node under it
    //
    // it is split from HeadlessCheck for the reason CONVENTIONS.md gives: a helper that needs no
    // engine should be reachable from game.tests, and this one is the whole reason the base class
    // exists. the wording and the exit code are the contract check-*.ps1 and CI read, so they are
    // the part worth pinning - the Node around it only decides when to say it
    public static class CheckVerdict
    {
        public const int Pass = 0;

        public const int Fail = 1;

        public static int ExitCode(bool passed) => passed ? Pass : Fail;

        // "fairness check passed" / "FAIRNESS CHECK FAILED with 2 problems"
        //
        // the shout on failure is not decoration: these run headless with thousands of lines of
        // sweep output above them, and the eye finds the capitals
        //
        // <paramref name="detail"/> is appended to the failure line only. "passed with 0 problems"
        // is noise, and a bare "FAILED" tells a reader nothing about where to look
        public static string Line(string subject, bool passed, string detail = null) =>
            passed
                ? $"{subject} check passed"
                : $"{subject.ToUpperInvariant()} CHECK FAILED" +
                  (string.IsNullOrEmpty(detail) ? "" : " " + detail);

        // "with 1 problem" / "with 2 problems", for a check whose verdict is a count
        public static string Counted(int problems) =>
            $"with {problems} problem{(problems == 1 ? "" : "s")}";
    }
}

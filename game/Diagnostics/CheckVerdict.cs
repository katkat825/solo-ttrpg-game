namespace Game.Diagnostics
{
    public static class CheckVerdict
    {
        public const int Pass = 0;

        public const int Fail = 1;

        public static int ExitCode(bool passed) => passed ? Pass : Fail;

        // the capitals on failure are for the eye scanning thousands of headless lines
        public static string Line(string subject, bool passed, string detail = null) =>
            passed
                ? $"{subject} check passed"
                : $"{subject.ToUpperInvariant()} CHECK FAILED" +
                  (string.IsNullOrEmpty(detail) ? "" : " " + detail);

        public static string Counted(int problems) =>
            $"with {problems} problem{(problems == 1 ? "" : "s")}";
    }
}

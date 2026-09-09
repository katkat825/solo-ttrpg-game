using Godot;

namespace Game.Diagnostics
{
    // what every headless check has in common: how it names a problem, how it says what it
    // found, and what it exits with. the measuring is the subclass's business and nothing here
    // knows anything about dice or locales
    //
    // it exists because the scaffold was hand-rolled twice (SEAMS.md section 5) - QuitWhenDone,
    // the verdict line and GetTree().Quit(pass ? 0 : 1) written out in both DiceFairness and
    // LocaleAudit, and the third check would have been a third copy. Phases B, C and P each want
    // one: a board sanity sweep, a combat replay check, a campaign-load audit
    //
    // THE EXIT-CODE CONVENTION IS THE POINT. these run from check-*.ps1 and are meant to be
    // wired into a pre-commit hook or CI, so 0 means the thing is fine and 1 means it isn't -
    // and that has to be one sentence in one place, because a check that exits 0 on failure is
    // worse than no check at all
    //
    // the sentence itself is CheckVerdict, which needs no engine and so has tests in game.tests.
    // this class is only when to say it
    public abstract partial class HeadlessCheck : Node
    {
        // off for a check being driven from somewhere that wants to keep running afterwards
        [Export] public bool QuitWhenDone { get; set; } = true;

        // what is being checked, lowercase and one word - "fairness", "locale"
        // the verdict lines are built from it, so "fairness check passed" and
        // "FAIRNESS CHECK FAILED" cannot drift apart or from the exit code beside them
        protected abstract string Subject { get; }

        // how many problems have been reported, for a check that counts them
        protected int Problems { get; private set; }

        // one problem, said the same way by every check, and impossible to report without
        // it counting toward the verdict
        protected void Problem(string message)
        {
            Problems++;
            GD.Print("  FAIL  " + message);
        }

        // for a check whose verdict is the count of problems it reported
        protected void Finish() => Finish(Problems == 0, CheckVerdict.Counted(Problems));

        // for a check that decides for itself - the fairness sweep's verdict comes off a
        // chi-squared, which is not a count of anything
        //
        // <paramref name="detail"/> is appended to the failure line only, because "passed with
        // 0 problems" is noise and "FAILED" on its own tells you nothing about where to look
        protected void Finish(bool passed, string detail = null)
        {
            GD.Print(CheckVerdict.Line(Subject, passed, detail));

            if (QuitWhenDone) GetTree().Quit(CheckVerdict.ExitCode(passed));
        }
    }
}

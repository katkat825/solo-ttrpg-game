using Godot;

namespace Game.Diagnostics
{
    public abstract partial class HeadlessCheck : Node
    {
        [Export] public bool QuitWhenDone { get; set; } = true;

        // one lowercase word; the verdict lines are built from it so they cannot drift from the exit code
        protected abstract string Subject { get; }

        protected int Problems { get; private set; }

        // reporting a problem always counts it toward the verdict, so the two cannot diverge
        protected void Problem(string message)
        {
            Problems++;
            GD.Print("  FAIL  " + message);
        }

        protected void Finish() => Finish(Problems == 0, CheckVerdict.Counted(Problems));

        // for a check that decides for itself, like the fairness chi-squared, not a count
        protected void Finish(bool passed, string detail = null)
        {
            GD.Print(CheckVerdict.Line(Subject, passed, detail));

            if (QuitWhenDone) GetTree().Quit(CheckVerdict.ExitCode(passed));
        }
    }
}

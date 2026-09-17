using Godot;

namespace Game.Diagnostics
{
    public abstract partial class HeadlessCheck : Node
    {
        [Export] public bool QuitWhenDone { get; set; } = true;

        // one lowercase word; the verdict lines are built from it so they cannot drift from the exit code
        protected abstract string Subject { get; }

        protected int Problems { get; private set; }

        protected int Cautions { get; private set; }

        // reporting a problem always counts it toward the verdict, so the two cannot diverge
        protected void Problem(string message)
        {
            Problems++;
            GD.Print("  FAIL  " + message);
        }

        // A THING THAT WAS DONE, THAT SOMEBODY OUGHT TO READ (PLACES_AND_PERSISTENCE.md section 6).
        // It is counted and printed and it does not fail the check: the World layer needed to be
        // able to say "this quest's turn-in NPC is attackable, so killing him may make it
        // impossible" without refusing a campaign whose author meant exactly that.
        protected void Caution(string message)
        {
            Cautions++;
            GD.Print("  WARN  " + message);
        }

        protected void Finish() =>
            Finish(Problems == 0,
                   CheckVerdict.Counted(Problems) +
                   (Cautions > 0 ? $", {Cautions} caution(s)" : ""));

        // for a check that decides for itself, like the fairness chi-squared, not a count
        protected void Finish(bool passed, string detail = null)
        {
            GD.Print(CheckVerdict.Line(Subject, passed, detail));

            if (QuitWhenDone) GetTree().Quit(CheckVerdict.ExitCode(passed));
        }
    }
}

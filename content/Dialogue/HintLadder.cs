using System;
using System.Collections.Generic;

namespace Content.Dialogue
{
    // Hints are PULLED, not pushed (CORE_RULES.md section 12, W4). The player asks; the companion
    // never volunteers the solution. This is the counting half of that, and it is pure so the
    // escalation can be tested without a table: three rungs on repeat asks, then an in-character
    // sign of being over-asked, which is the self-regulation that keeps the system in the fiction.
    //
    // It has NO mechanical effect and gates nothing. A hint is texture; the information behind it
    // must always be reachable another way, and the campaign validator says so where it can.
    public sealed class HintLadder
    {
        // observation, nudge, then something close to the answer
        public const int Rungs = 3;

        readonly Dictionary<string, int> _asked = new Dictionary<string, int>(StringComparer.Ordinal);

        // how many times each problem has been asked about, so a save can carry it
        public IReadOnlyDictionary<string, int> Asked => _asked;

        public int AsksAbout(string problem) =>
            problem != null && _asked.TryGetValue(problem, out int times) ? times : 0;

        // restoring rather than replaying: a resumed game remembers it has already been asked twice
        public void Restore(string problem, int times)
        {
            if (problem == null || times <= 0) return;

            _asked[problem] = times;
        }

        public void Forget(string problem)
        {
            if (problem != null) _asked.Remove(problem);
        }

        // asking again about a problem you have solved starts over; a new room is a new question
        public void Solved(string problem) => Forget(problem);

        // the rung this ask lands on: 1, 2, 3, and then Overasked for every ask after that
        public Ask Next(string problem)
        {
            if (string.IsNullOrEmpty(problem)) return Ask.Nothing;

            int times = AsksAbout(problem) + 1;

            _asked[problem] = times;

            return times <= Rungs ? new Ask(times) : Ask.TooMany;
        }

        // what the ladder would answer without spending the ask; for a UI that greys out the cord
        public Ask Peek(string problem)
        {
            if (string.IsNullOrEmpty(problem)) return Ask.Nothing;

            int times = AsksAbout(problem) + 1;

            return times <= Rungs ? new Ask(times) : Ask.TooMany;
        }

        public override string ToString() => $"{_asked.Count} problems asked about";
    }

    // a rung, or the answer that is not one
    public readonly struct Ask
    {
        public Ask(int rung)
        {
            Rung = rung;
            Overasked = false;
        }

        Ask(int rung, bool overasked)
        {
            Rung = rung;
            Overasked = overasked;
        }

        // 1..HintLadder.Rungs, or 0 for nothing at all
        public int Rung { get; }

        // the fiend charges more, the hound gets anxious, the saint gets disappointed
        public bool Overasked { get; }

        public bool Answers => Rung > 0;

        public static readonly Ask Nothing = new Ask(0, false);

        public static readonly Ask TooMany = new Ask(0, true);

        public override string ToString() =>
            Overasked ? "over-asked" : Rung > 0 ? $"rung {Rung}" : "nothing to ask about";
    }
}

using System.Collections.Generic;
using Content.Places;

namespace Content.World
{
    public abstract class Happened
    {
        readonly List<Cue> _cues = new List<Cue>();

        readonly List<Trigger> _triggers = new List<Trigger>();

        readonly List<string> _facts = new List<string>();

        // in the author's order; a different order tells a different story
        public IReadOnlyList<Cue> Cues => _cues;

        // only the ones that move the story - next, goto, ends; set and clear already happened
        public IReadOnlyList<Trigger> Triggers => _triggers;

        public IReadOnlyList<string> Facts => _facts;

        public void Add(Cue cue)
        {
            if (cue != null) _cues.Add(cue);
        }

        public void Add(Trigger trigger)
        {
            if (trigger != null) _triggers.Add(trigger);
        }

        public void Add(string fact)
        {
            if (!string.IsNullOrEmpty(fact) && !_facts.Contains(fact)) _facts.Add(fact);
        }

        // whichever of next / goto / ends the author hung here, or null for most moments
        public Trigger Moves
        {
            get
            {
                foreach (Trigger trigger in _triggers)
                    if (trigger.ThenDo is Then.Next or Then.Goto or Then.Ends) return trigger;

                return null;
            }
        }

        protected string Said() =>
            (_cues.Count > 0 ? $", {_cues.Count} cue(s)" : "") +
            (_facts.Count > 0 ? $", now true: {string.Join(", ", _facts)}" : "") +
            (Moves != null ? $", and then {Moves}" : "");
    }
}

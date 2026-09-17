using System;
using System.Collections.Generic;
using Content.Places;

namespace Content.World
{
    // fires once when its fact becomes true; without the guard every place-rebuild would re-fire it
    sealed class Reactions
    {
        readonly HashSet<string> _fired = new HashSet<string>(StringComparer.Ordinal);

        public void Arrived() => _fired.Clear();

        public IEnumerable<Cue> Cues(Place place, Facts facts, When moment)
        {
            foreach (Cue cue in place.CuesFor(moment))
            {
                if (moment == When.Fact)
                {
                    if (!facts.Is(cue.Fact)) continue;

                    if (!_fired.Add("cue:" + cue.Id + ":" + cue.Fact)) continue;
                }

                yield return cue;
            }
        }

        public IEnumerable<Trigger> Triggers(Place place, Facts facts)
        {
            foreach (Trigger trigger in place.Triggers)
            {
                if (trigger.WhenIt != When.Fact) continue;

                if (!facts.Is(trigger.Fact)) continue;

                if (!_fired.Add("trigger:" + trigger)) continue;

                yield return trigger;
            }
        }
    }
}

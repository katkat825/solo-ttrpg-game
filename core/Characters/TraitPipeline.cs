using System.Collections.Generic;
using Core.Dice;

namespace Core.Characters
{
    // base attribute dice, plus an ordered list of modifiers that says how they got where
    // they are. one attribute's current die is composed from its base and everything pressing
    // on it, and nothing else can write it
    //
    // this came off Actor in F3. it was two parallel dictionaries and a Recalculate that
    // replayed conditions, with capacity for exactly one step per attribute and no record of
    // who spent it. the three things that broke the moment a second source arrived are the
    // three properties this holds instead:
    //
    //   ORDER IS IRRELEVANT.    steps are summed and the die moves once, so a step up before a
    //                           step down and after it give the same die. replaying moves does
    //                           not have that property once anything can move a die upward
    //   SATURATION IS VISIBLE.  the ladder clamps and the leftover is kept, so three steps down
    //                           from d6 is a d4 with two refused, not just a d4
    //   PROVENANCE IS KEPT.     every modifier carries its source, so one can be taken off
    //                           without disturbing the others
    //
    // the invariant to preserve, and the one that fixed the Winded-and-un-debuffed bug in
    // 2026-08-20: every write to _current is inside Recalculate. there is one path, not two
    public sealed class TraitPipeline
    {
        readonly Dictionary<Attr, Die> _base = new Dictionary<Attr, Die>();
        readonly Dictionary<Attr, Die> _current = new Dictionary<Attr, Die>();
        readonly Dictionary<Attr, int> _refused = new Dictionary<Attr, int>();
        readonly List<TraitModifier> _modifiers = new List<TraitModifier>();

        // insertion order, which is presentation's business and never the arithmetic's
        public IReadOnlyList<TraitModifier> Modifiers => _modifiers;

        public Die Base(Attr a) => _base.TryGetValue(a, out var d) ? d : Die.None;

        public Die Current(Attr a) => _current.TryGetValue(a, out var d) ? d : Die.None;

        // steps the ladder could not deliver - negative below the d4 floor, positive above the
        // d12 ceiling, zero when the whole stack was absorbed
        //
        // this is the number that makes saturation observable. without it "two stacks" and
        // "three stacks" are the same d4 and nobody can tell the rule from a bug
        public int Refused(Attr a) => _refused.TryGetValue(a, out int n) ? n : 0;

        public void SetBase(Attr a, Die d)
        {
            _base[a] = d;
            Recalculate();
        }

        public void Add(TraitModifier m)
        {
            _modifiers.Add(m);
            Recalculate();
        }

        public bool Has(ModifierSource source) => _modifiers.Exists(m => m.Source == source);

        // everything from one source, in one call - "take the ring off" is one act however many
        // dice the ring was moving. returns how many were removed
        public int RemoveAllFrom(ModifierSource source)
        {
            int removed = _modifiers.RemoveAll(m => m.Source == source);
            if (removed > 0) Recalculate();
            return removed;
        }

        void Recalculate()
        {
            _current.Clear();
            _refused.Clear();

            foreach (KeyValuePair<Attr, Die> kv in _base) _current[kv.Key] = kv.Value;

            // sum first, move once - see CORE_RULES.md section 9, "Saturation"
            var net = new Dictionary<Attr, int>();

            foreach (TraitModifier m in _modifiers)
            {
                // an attribute with no die is not on the ladder, so nothing can move it and
                // nothing can floor it. a foe with no Grace statblock takes Reeling and is
                // unaffected - the alternative drops every mook that catches a Condition its
                // statblock never mentioned
                if (!_base.TryGetValue(m.Attribute, out Die b) || !b.IsReal()) continue;

                net[m.Attribute] = (net.TryGetValue(m.Attribute, out int n) ? n : 0) + m.Steps;
            }

            foreach (KeyValuePair<Attr, int> kv in net)
            {
                _current[kv.Key] = _base[kv.Key].StepBy(kv.Value, out int delivered);
                _refused[kv.Key] = kv.Value - delivered;
            }
        }
    }
}

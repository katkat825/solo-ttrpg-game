using System.Collections.Generic;
using Core.Dice;

namespace Core.Characters
{
    // order-independent (steps summed, moved once), saturation stays visible, each modifier keeps its source
    // the invariant: every write to _current is inside Recalculate - one path, not two
    public sealed class TraitPipeline
    {
        readonly Dictionary<Attr, Die> _base = new Dictionary<Attr, Die>();
        readonly Dictionary<Attr, Die> _current = new Dictionary<Attr, Die>();
        readonly Dictionary<Attr, int> _refused = new Dictionary<Attr, int>();
        readonly List<TraitModifier> _modifiers = new List<TraitModifier>();

        public IReadOnlyList<TraitModifier> Modifiers => _modifiers;

        public Die Base(Attr a) => _base.TryGetValue(a, out var d) ? d : Die.None;

        public Die Current(Attr a) => _current.TryGetValue(a, out var d) ? d : Die.None;

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

            // sum first, move once
            var net = new Dictionary<Attr, int>();

            foreach (TraitModifier m in _modifiers)
            {
                // no base die means not on the ladder - a foe with no Grace shrugs off Reeling instead of dropping
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

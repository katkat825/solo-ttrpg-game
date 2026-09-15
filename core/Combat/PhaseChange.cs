using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Dice;

namespace Core.Combat
{
    // a positive-step TraitModifier - the pipeline makes Winded-then-phase equal phase-then-Winded
    // once, and on crossing the threshold - a Dread healed back above half gets no second phase
    public sealed class PhaseChange
    {
        readonly Actor _actor;

        readonly TraitModifier[] _rerated;

        public PhaseChange(Actor actor, ITargetSelector then, params TraitModifier[] rerated)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _rerated = rerated ?? Array.Empty<TraitModifier>();

            Then = then;
        }

        public Actor Actor => _actor;

        public ITargetSelector Then { get; }

        public IReadOnlyList<TraitModifier> Rerated => _rerated;

        public bool Turned { get; private set; }

        public bool Due => !Turned && !_actor.IsDown && _actor.Vigor * 2 <= _actor.MaxVigor;

        public bool Turn()
        {
            if (Turned) return false;

            Turned = true;

            foreach (TraitModifier m in _rerated) _actor.AddModifier(m);

            return true;
        }


        public static readonly ModifierSource Source = ModifierSource.Effect("phase");

        public static PhaseChange Standard(Actor dread)
        {
            var rerated = new List<TraitModifier>();

            foreach (Attr a in Enum.GetValues<Attr>())
                if (dread.BaseAttribute(a).IsReal())
                    rerated.Add(new TraitModifier(Source, a, +1));

            return new PhaseChange(dread, new StrongestFirstSelector(), rerated.ToArray());
        }

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            $"{_actor.DebugName} phase {(Turned ? "turned" : Due ? "due" : "waiting")} " +
            $"at {_actor.Vigor}/{_actor.MaxVigor}";
    }
}

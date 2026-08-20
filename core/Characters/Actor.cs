using System;
using System.Collections.Generic;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Core.Characters
{
    // anything that can act or be hit - the hero, a Rabble, a Rival, a Dread
    // attributes are dice, conditions step them down a size
    // names itself with a localization key, never with text
    // mutable - damage and conditions change it in place
    public sealed class Actor
    {
        // stable id, e.g. "barbarian" - not display text
        public string Id { get; }

        public Tier Tier { get; }

        // which of several identical foes this is, 1-based - 0 means unnumbered
        // presentation feeds it to NameKey as {0}
        // never glue a number onto a translated name
        public int Ordinal { get; set; }

        public string NameKey => Ordinal > 0
            ? KeyConventions.ActorNameNumbered(Id)
            : KeyConventions.ActorName(Id);

        // DEVELOPER ONLY - logs, test failures, the sim
        // not localized and never will be, so it must never reach the screen
        public string DebugName => Ordinal > 0 ? $"{Id}#{Ordinal}" : Id;

        readonly Dictionary<Attr, Die> _base = new Dictionary<Attr, Die>();
        readonly Dictionary<Attr, Die> _current = new Dictionary<Attr, Die>();
        readonly Dictionary<Skill, Die> _skills = new Dictionary<Skill, Die>();
        readonly List<Condition> _conditions = new List<Condition>();

        public Die Weapon { get; private set; } = Die.None;

        public string WeaponId { get; private set; } = "unarmed";

        public string WeaponKey => KeyConventions.GearName(WeaponId);

        public int Vigor { get; private set; }
        public int MaxVigor { get; }

        // the number an attacker's pool must beat
        // by far the strongest balance lever - tune encounters here, not with Vigor
        public int Defense { get; set; }

        public int Nerve { get; set; }
        public int ActionsPerRound { get; set; }

        public IReadOnlyList<Condition> Conditions => _conditions;
        public bool IsDown => Vigor <= 0;

        public Actor(string id, int maxVigor, int defense, Tier tier = Tier.Rival)
        {
            Id = id;
            Tier = tier;
            MaxVigor = maxVigor;
            Vigor = maxVigor;
            Defense = defense;
            Nerve = tier == Tier.Rabble ? 0 : 3;
            ActionsPerRound = tier == Tier.Dread ? 2 : 1;
        }

        // ---- construction ----

        // sets the BASE die and lets the pipeline derive the current one
        // writing _current directly here erased any active condition's effect while leaving the
        // condition in the list, so the actor was Winded and un-debuffed at the same time, and
        // ClearCondition would later step it down a second time
        // every write to _current goes through Recalculate, so there is one path and not two
        public Actor With(Attr a, Die d)
        {
            _base[a] = d;
            Recalculate();
            return this;
        }

        public Actor With(Skill s, Die d)
        {
            _skills[s] = d;
            return this;
        }

        public Actor WithWeapon(string weaponId, Die d)
        {
            WeaponId = weaponId;
            Weapon = d;
            return this;
        }

        public Actor Numbered(int ordinal)
        {
            Ordinal = ordinal;
            return this;
        }

        // ---- traits ----

        public Die Attribute(Attr a) => _current.TryGetValue(a, out var d) ? d : Die.None;
        public Die BaseAttribute(Attr a) => _base.TryGetValue(a, out var d) ? d : Die.None;
        public Die SkillDie(Skill s) => _skills.TryGetValue(s, out var d) ? d : Die.None;

        // attribute + skill (if trained) + gear
        // each die carries the key of the trait that contributed it
        // so the tray can label dice without knowing any rules
        public Pool BuildPool(Attr a, Skill s = Skill.None, bool useWeapon = true)
        {
            var p = new Pool();
            p.Add(a.Key(), Attribute(a));
            if (s != Skill.None) p.Add(s.Key(), SkillDie(s));
            if (useWeapon) p.Add(WeaponKey, Weapon);
            return p;
        }

        // ---- conditions ----

        public bool HasCondition(Condition c) => _conditions.Contains(c);

        // a Condition steps the attribute it affects down one size
        // this stepped _current in place until 2026-08-20, which meant applying a condition and
        // clearing one computed the same value two different ways - identical today only because
        // StepDown saturates at D4 and nothing can buff. add one buff and they diverge, silently
        // and only after a particular sequence of play. one path instead
        public bool ApplyCondition(Condition c)
        {
            if (_conditions.Contains(c)) return false;
            _conditions.Add(c);
            Recalculate();
            return true;
        }

        public bool ClearCondition(Condition c)
        {
            if (!_conditions.Remove(c)) return false;
            Recalculate();
            return true;
        }

        void Recalculate()
        {
            foreach (var kv in _base) _current[kv.Key] = kv.Value;

            foreach (var c in _conditions)
            {
                var a = c.Affects();
                if (_current.TryGetValue(a, out var d)) _current[a] = d.StepDown();
            }
        }

        // ---- damage ----

        // Rabble ignore the vigor track entirely - any hit removes them
        // crossing 2/3 and 1/3 of max vigor applies Winded then Reeling
        // returns what was newly applied, so callers can report it
        // without the Actor knowing anything about observers
        public IReadOnlyList<Condition> Damage(int amount)
        {
            if (Tier == Tier.Rabble)
            {
                Vigor = 0;
                return Array.Empty<Condition>();
            }

            Vigor -= amount;

            List<Condition> applied = null;

            if (Vigor <= MaxVigor * 2 / 3.0 && ApplyCondition(Condition.Winded))
                (applied ??= new List<Condition>()).Add(Condition.Winded);

            if (Vigor <= MaxVigor / 3.0 && ApplyCondition(Condition.Reeling))
                (applied ??= new List<Condition>()).Add(Condition.Reeling);

            return applied ?? (IReadOnlyList<Condition>)Array.Empty<Condition>();
        }

        public void Heal(int amount) => Vigor = Math.Min(MaxVigor, Vigor + amount);

        // DEVELOPER ONLY - not localized, never shown to a player
        public override string ToString() =>
            $"{DebugName} [{Tier}] vigor {Vigor}/{MaxVigor} def {Defense}" +
            (_conditions.Count > 0 ? " (" + string.Join(", ", _conditions) + ")" : "");
    }
}

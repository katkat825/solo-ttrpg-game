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

        // base dice, everything pressing on them, and the arithmetic that composes the two
        // it came off this class in F3 - see TraitPipeline for what it holds and why
        readonly TraitPipeline _traits = new TraitPipeline();
        readonly Dictionary<Skill, Die> _skills = new Dictionary<Skill, Die>();
        readonly List<Condition> _conditions = new List<Condition>();

        public Die Weapon { get; private set; } = Die.None;

        public string WeaponId { get; private set; } = "unarmed";

        public string WeaponKey => KeyConventions.GearName(WeaponId);

        // clamped at 0 in the one place it is written, so "how much vigor" has a single answer
        // it used to be able to go negative and CombatEngine papered over it with Math.Max(0, ...)
        // at the point it reported the result, which is two answers depending on who you asked
        public int Vigor { get; private set; }
        public int MaxVigor { get; }

        // the number an attacker's pool must beat
        // by far the strongest balance lever - tune encounters here, not with Vigor
        public int Defense { get; set; }

        public int Nerve { get; set; }
        public int ActionsPerRound { get; set; }

        public IReadOnlyList<Condition> Conditions => _conditions;

        // every modifier on this actor, with its source - presentation and save/load read it
        public IReadOnlyList<TraitModifier> Modifiers => _traits.Modifiers;

        // CORE_RULES.md section 9: "take a Condition you can't absorb and you're down"
        // made mechanical - the ladder refused a step DOWN on an attribute a Condition is
        // pressing on, so that Condition bought nothing and there was nothing left to give
        //
        // gear alone cannot do this. a cursed ring can pin a die at d4 all day and you keep
        // fighting; it is a Condition landing on a die with no room that finishes you
        public bool IsOverwhelmed
        {
            get
            {
                foreach (var c in _conditions)
                    if (_traits.Refused(c.Affects()) < 0) return true;

                return false;
            }
        }

        public bool IsDown => Vigor <= 0 || IsOverwhelmed;

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
        // writing the current die directly here erased any active condition's effect while
        // leaving the condition in the list, so the actor was Winded and un-debuffed at the
        // same time, and ClearCondition would later step it down a second time
        // the pipeline owns the only write, so there is one path and not two
        public Actor With(Attr a, Die d)
        {
            _traits.SetBase(a, d);
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

        public Die Attribute(Attr a) => _traits.Current(a);
        public Die BaseAttribute(Attr a) => _traits.Base(a);
        public Die SkillDie(Skill s) => _skills.TryGetValue(s, out var d) ? d : Die.None;

        // steps the ladder refused on this attribute - 0 when the whole stack was absorbed,
        // negative when it asked for more than the d4 floor has. see TraitPipeline.Refused
        public int Saturation(Attr a) => _traits.Refused(a);

        // ---- modifiers ----

        // feats, gear and effects arrive in Phases P and R; the pipeline that holds them is here
        public void AddModifier(TraitModifier m) => _traits.Add(m);

        // "take the ring off, keep raging" - by source, so nothing else is disturbed
        public int RemoveModifiers(ModifierSource source) => _traits.RemoveAllFrom(source);

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

        // a Condition is one modifier like any other - one step down, sourced to itself, so
        // clearing it takes off exactly what it put on and nothing else
        // it stepped the current die in place until 2026-08-20, which meant applying a condition
        // and clearing one computed the same value two different ways
        public bool ApplyCondition(Condition c)
        {
            if (_conditions.Contains(c)) return false;

            _conditions.Add(c);
            _traits.Add(new TraitModifier(ModifierSource.FromCondition(c), c.Affects(), -1));
            return true;
        }

        public bool ClearCondition(Condition c)
        {
            if (!_conditions.Remove(c)) return false;

            _traits.RemoveAllFrom(ModifierSource.FromCondition(c));
            return true;
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

            Vigor = Math.Max(0, Vigor - amount);

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

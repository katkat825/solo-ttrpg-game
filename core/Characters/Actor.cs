using System;
using System.Collections.Generic;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Core.Characters
{
    public sealed class Actor
    {
        public string Id { get; }

        public Tier Tier { get; }

        public int Ordinal { get; set; }

        public string NameKey => Ordinal > 0
            ? KeyConventions.ActorNameNumbered(Id)
            : KeyConventions.ActorName(Id);

        // debug only, never localized - keep it off the screen
        public string DebugName => Ordinal > 0 ? $"{Id}#{Ordinal}" : Id;

        readonly TraitPipeline _traits = new TraitPipeline();
        readonly Dictionary<Skill, Die> _skills = new Dictionary<Skill, Die>();
        readonly List<Condition> _conditions = new List<Condition>();

        public Gear Wielded { get; private set; } = Gear.Nothing;

        public Gear Worn { get; private set; } = Gear.Nothing;

        public Die Weapon { get; private set; } = Die.None;

        public string WeaponId => Wielded.Id;

        public string WeaponKey => Wielded.NameKey;

        public int Vigor { get; private set; }
        public int MaxVigor { get; }

        public int BaseDefense { get; set; }

        public int Defense
        {
            get => BaseDefense + Worn.Defense;
            set => BaseDefense = value;
        }

        public int Nerve { get; private set; }

        public int NerveCap { get; set; }

        public bool SpendNerve(int amount = 1)
        {
            if (amount <= 0 || Nerve < amount) return false;

            Nerve -= amount;
            return true;
        }

        public int GainNerve(int amount = 1)
        {
            if (amount <= 0) return 0;

            int room = Math.Max(0, NerveCap - Nerve);
            int landed = Math.Min(room, amount);

            Nerve += landed;
            return landed;
        }

        public void RestoreNerve(int to) => Nerve = Math.Clamp(to, 0, NerveCap);

        // defaulted from the tier; the hero's count is CombatOptions.HeroActionsPerRound, not this
        public int ActionsPerRound { get; set; }

        public int ReactionsPerRound { get; set; }

        public string Behaviour { get; set; }

        public IReadOnlyList<Condition> Conditions => _conditions;

        public IReadOnlyList<TraitModifier> Modifiers => _traits.Modifiers;

        // overwhelmed = a Condition's step down was refused, so it had nothing left to give
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
            NerveCap = tier.NerveCap();
            Nerve = tier.StartingNerve();
            ActionsPerRound = tier.ActionsPerRound();
            ReactionsPerRound = tier.ReactionsPerRound();
        }


        // sets the base die and lets the pipeline derive the current one - one write path, so apply and clear can't disagree
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

        public Actor WithWeapon(string weaponId, Die d) => Wielding(new Gear(weaponId, d));

        public Actor Wielding(Gear gear)
        {
            Wielded = gear ?? Gear.Nothing;
            Weapon = Wielded.Die;
            BaseWeapon = Wielded.Die;
            return this;
        }

        public Actor Wearing(Gear gear)
        {
            Worn = gear ?? Gear.Nothing;
            return this;
        }


        public Die BaseWeapon { get; private set; } = Die.None;

        public int Notches { get; private set; }

        public bool NotchGear()
        {
            if (!Weapon.IsReal()) return false;

            Die was = Weapon;
            Weapon = Weapon.StepDown();

            if (Weapon == was) return false;

            Notches++;
            return true;
        }

        public void MendGear()
        {
            Weapon = BaseWeapon;
            Notches = 0;
        }

        public IReadOnlyList<Condition> Inflicts => Wielded.Inflicts;


        readonly List<string> _satchel = new List<string>();

        public IReadOnlyList<string> Satchel => _satchel;

        public void Carry(string itemId)
        {
            if (!string.IsNullOrWhiteSpace(itemId)) _satchel.Add(itemId);
        }

        public bool Drop(string itemId) => _satchel.Remove(itemId);


        public static readonly ModifierSource StrainSource = ModifierSource.Effect("strain");

        public int Strain { get; private set; }

        public void AddStrain(int steps = 1)
        {
            for (int i = 0; i < steps; i++)
            {
                AddModifier(new TraitModifier(StrainSource, Attr.Heart, -1));
                Strain++;
            }
        }

        public int MendStrain()
        {
            int was = Strain;

            Strain = 0;
            RemoveModifiers(StrainSource);

            return was;
        }

        public Actor Numbered(int ordinal)
        {
            Ordinal = ordinal;
            return this;
        }


        public Die Attribute(Attr a) => _traits.Current(a);
        public Die BaseAttribute(Attr a) => _traits.Base(a);
        public Die SkillDie(Skill s) => _skills.TryGetValue(s, out var d) ? d : Die.None;

        public int Saturation(Attr a) => _traits.Refused(a);


        public void AddModifier(TraitModifier m) => _traits.Add(m);

        public int RemoveModifiers(ModifierSource source) => _traits.RemoveAllFrom(source);

        public Pool BuildPool(Attr a, Skill s = Skill.None, bool useWeapon = true)
        {
            var p = new Pool();
            p.Add(a.Key(), Attribute(a));
            if (s != Skill.None) p.Add(s.Key(), SkillDie(s));

            if (useWeapon && Wielded.Helps(s)) p.Add(WeaponKey, Weapon);

            return p;
        }


        public bool HasCondition(Condition c) => _conditions.Contains(c);

        // a Condition is one sourced modifier - clearing it removes exactly what applying it added
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


        // the amount<=0 guard matters: without it Damage(0) - a miss - zeroed a Rabble
        public IReadOnlyList<Condition> Damage(int amount)
        {
            if (amount <= 0) return Array.Empty<Condition>();

            if (!Tier.HasHealthTrack())
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

        // not Damage(MaxVigor - to): that would re-fire the Winded/Reeling thresholds a save already recorded
        public void RestoreVigor(int to) => Vigor = Math.Clamp(to, 0, MaxVigor);

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            $"{DebugName} [{Tier}] vigor {Vigor}/{MaxVigor} def {Defense}" +
            (_conditions.Count > 0 ? " (" + string.Join(", ", _conditions) + ")" : "");
    }
}

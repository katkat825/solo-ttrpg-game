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

        // WHAT IS IN ITS HANDS, and what is on its back (P1). Two slots and not a bag: a hero
        // holds one thing and wears one thing, and CORE_RULES.md section 13 keeps a second
        // inventory dead. Both default to nothing, which is a real state - an unarmed, unarmoured
        // actor throws a smaller pool and has the Defense its statblock gave it
        public Gear Wielded { get; private set; } = Gear.Nothing;

        public Gear Worn { get; private set; } = Gear.Nothing;

        // the gear die as it is NOW - notched down by a Trouble, or whatever it was made as
        public Die Weapon { get; private set; } = Die.None;

        public string WeaponId => Wielded.Id;

        public string WeaponKey => Wielded.NameKey;

        // clamped at 0 in the one place it is written, so "how much vigor" has a single answer
        // it used to be able to go negative and CombatEngine papered over it with Math.Max(0, ...)
        // at the point it reported the result, which is two answers depending on who you asked
        public int Vigor { get; private set; }
        public int MaxVigor { get; }

        // the number an attacker's pool must beat
        // by far the strongest balance lever - tune encounters here, not with Vigor
        //
        // WHAT THE STATBLOCK SAID, plus whatever is worn (P1). Split in two so that taking the
        // plate off gives the number back exactly, and so the sim can move the statblock's own
        // figure without an armoured actor's total drifting from it
        public int BaseDefense { get; set; }

        public int Defense
        {
            get => BaseDefense + Worn.Defense;
            set => BaseDefense = value;
        }

        // CORE_RULES.md section 7. Read by Encounter, which is what makes it mean something -
        // it was set and never read anywhere in the repo until Phase C (SEAMS.md section 3)
        public int Nerve { get; private set; }

        // five, and a Rabble has none at all. Settable because a campaign's hero may not be an
        // ordinary one, and defaulted from the tier so nothing has to say it twice
        public int NerveCap { get; set; }

        // one act of heroic effort. false when there is none to spend, which is the caller's
        // answer to "can I" as well as "do it" - there is no separate question worth asking
        public bool SpendNerve(int amount = 1)
        {
            if (amount <= 0 || Nerve < amount) return false;

            Nerve -= amount;
            return true;
        }

        // and taking a complication to bank one. Returns how much actually landed, which is 0 at
        // the cap - a player at five who accepts a Trouble for nothing has been robbed, so the
        // caller is told and can offer the shrug instead
        public int GainNerve(int amount = 1)
        {
            if (amount <= 0) return 0;

            int room = Math.Max(0, NerveCap - Nerve);
            int landed = Math.Min(room, amount);

            Nerve += landed;
            return landed;
        }

        // for a rest, and for a check that wants a known starting state (CORE_RULES.md section 11)
        public void RestoreNerve(int to) => Nerve = Math.Clamp(to, 0, NerveCap);

        // how many actions this actor gets in a round. defaulted from the tier and settable,
        // because a campaign's boss may not be an ordinary Dread. the HERO's count is not here:
        // it is CombatOptions.HeroActionsPerRound, the balance lever SIMULATION.md section 2
        // measured, and Encounter reads that for the hero and this for everyone else
        public int ActionsPerRound { get; set; }

        // one reaction for a Dread, none for anything else. named here so C3's minimal reaction
        // and C6's boss read the same number
        public int ReactionsPerRound { get; set; }

        // HOW IT PICKS WHO TO SWING AT, as a name out of the engine's own closed vocabulary
        // (`Core.Combat.Behaviours`). Null means the engine's default, which is what every
        // built-in statblock says and what `Encounter.BehaviourOf` falls back to.
        //
        // A NAME AND NOT A SELECTOR, because this is the field a campaign writes (Phase P) and
        // content is pure data, never code (ARCHITECTURE.md section 6). The seam stays open for
        // code - Encounter.Behaviour attaches a real ITargetSelector per foe, and C6's phase change
        // swaps one - and what a campaign gets is the right to CHOOSE from the ones that ship
        public string Behaviour { get; set; }

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
            NerveCap = tier.NerveCap();
            Nerve = tier.StartingNerve();
            ActionsPerRound = tier.ActionsPerRound();
            ReactionsPerRound = tier.ReactionsPerRound();
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

        public Actor WithWeapon(string weaponId, Die d) => Wielding(new Gear(weaponId, d));

        // IN ITS HANDS. The gear die joins the pool for the checks this gear is for, which for
        // everything the engine ships is all of them (Gear.Supports)
        public Actor Wielding(Gear gear)
        {
            Wielded = gear ?? Gear.Nothing;
            Weapon = Wielded.Die;
            BaseWeapon = Wielded.Die;
            return this;
        }

        // ON ITS BACK. Defense is the scalpel (SIMULATION.md section 3), so this is the single
        // most powerful thing a campaign can hand out and it is one number
        public Actor Wearing(Gear gear)
        {
            Worn = gear ?? Gear.Nothing;
            return this;
        }

        // ---- gear takes Conditions too (CORE_RULES.md section 9) ----

        // what the weapon was before anything happened to it, so a repair has something to go back
        // to and the tray can be asked how far it has fallen
        public Die BaseWeapon { get; private set; } = Die.None;

        public int Notches { get; private set; }

        // A NOTCHED BLADE DROPS ITS GEAR DIE. The engine's answer to a Trouble (Core.Combat.Trouble)
        // and the same mechanic as every other consequence in this game: the dice are the stats, so
        // the cost of something going wrong is a smaller die, and the player sees it on the next
        // throw rather than reading it anywhere.
        //
        // False at the d4 floor and false with nothing in hand, which is what makes the caller
        // look for somewhere else to put the consequence rather than quietly dropping it
        public bool NotchGear()
        {
            if (!Weapon.IsReal()) return false;

            Die was = Weapon;
            Weapon = Weapon.StepDown();

            if (Weapon == was) return false;

            Notches++;
            return true;
        }

        // a rest, an armourer, a campaign's own repair - none of which exist yet, and all of which
        // want the same one line
        public void MendGear()
        {
            Weapon = BaseWeapon;
            Notches = 0;
        }

        // WHAT A HIT WITH IT LEAVES BEHIND, beyond the damage - the "tagged attacks" half of
        // CORE_RULES.md section 9, which nothing could express until gear was data (P1). Empty for
        // everything the engine ships
        public IReadOnlyList<Condition> Inflicts => Wielded.Inflicts;

        // ---- and what has been picked up (P1) ----

        // WHAT IS IN THE SATCHEL: item ids, in the order they were found.
        //
        // NOT A SECOND INVENTORY. CORE_RULES.md section 13 keeps that dead, and this is not one -
        // it is the hero's only one, and a list of ids rather than a system: no weight, no slots,
        // no stacks, no sorting. Somewhere for a dropped item to go until Phase R gives the sheet
        // a place to show it and a way to swap it into a hand.
        //
        // IDS AND NOT `Gear`, because a save has to survive a campaign being uninstalled: a hero
        // carrying `ashfall.claw_necklace` in a world where ashfall is gone is carrying a name
        // nothing can resolve, and that degrades (P6) rather than failing to load
        readonly List<string> _satchel = new List<string>();

        public IReadOnlyList<string> Satchel => _satchel;

        public void Carry(string itemId)
        {
            if (!string.IsNullOrWhiteSpace(itemId)) _satchel.Add(itemId);
        }

        public bool Drop(string itemId) => _satchel.Remove(itemId);

        // ---- strain, the cost of channelling (CORE_RULES.md section 10) ----

        // ONE SOURCE, STACKING. Channelling steps the Heart die down until you rest, and the
        // pipeline F3 built is what makes that work without a special case: the steps sum and the
        // die moves once, the ladder clamps and remembers the overflow, and a rest takes off
        // exactly what channelling put on (`RemoveAllFrom`) without disturbing a Condition or a
        // cursed ring sitting on the same attribute.
        //
        // NOT A CONDITION, deliberately. A strained Heart at the d4 floor does not drop you -
        // "gear alone cannot do this... it is a Condition landing on a die with no room that
        // finishes you" (CORE_RULES.md section 9) - but it leaves you with nothing to absorb one
        // with, which is exactly the fragility to Shaken that section 10 promises
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

        // a Breather or a Camp - CORE_RULES.md section 11, both of which clear it
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

            // "the tool you're using" is not a tool you are not using (CORE_RULES.md section 1).
            // Everything the engine ships supports every check, so nothing here moves until a
            // campaign authors a weapon that names a skill
            if (useWeapon && Wielded.Helps(s)) p.Add(WeaponKey, Weapon);

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
        //
        // NOTHING HAPPENED IS NOTHING HAPPENING. this took no amount at all and zeroed a Rabble's
        // vigor regardless, so Actor.Damage(0) - a miss, a resisted blow, a Trouble that cost
        // nothing - removed one from the board. that is the disagreement SEAMS.md section 5
        // recorded between this method and CombatEngine.Attack, and the guard is half the fix;
        // the other half is that both now ask Tier.HasHealthTrack rather than each naming Rabble
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

        // PUT BACK WHERE IT WAS, for a save being loaded (CONTENT_PIPELINE.md P6) and for nothing
        // else. `RestoreNerve` above is the same method for the same reason and was written first.
        //
        // NOT `Damage(MaxVigor - to)`, which is the obvious way to do it and is wrong twice: it
        // would apply Winded and Reeling on the way down, and the save already knows which
        // Conditions this actor has - so the thresholds would fire a second time and a hero
        // restored to a third of his Vigor would come back carrying a Condition he had already
        // shrugged off. A save sets the state; it does not replay the fight that produced it
        public void RestoreVigor(int to) => Vigor = Math.Clamp(to, 0, MaxVigor);

        // DEVELOPER ONLY - not localized, never shown to a player
        public override string ToString() =>
            $"{DebugName} [{Tier}] vigor {Vigor}/{MaxVigor} def {Defense}" +
            (_conditions.Count > 0 ? " (" + string.Join(", ", _conditions) + ")" : "");
    }
}

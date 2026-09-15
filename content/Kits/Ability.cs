using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Content.Kits
{
    public sealed class Ability
    {
        public Ability(
            string id,
            Primitive primitive,
            Attr attribute,
            Skill skill,
            bool useGear,
            int against,
            bool againstDefense = false,
            Magnitude magnitude = Magnitude.Impact,
            int fixedMagnitude = 0,
            Cost cost = Cost.None,
            Target target = Target.Reach,
            IReadOnlyList<Effect> onHit = null,
            IReadOnlyList<Effect> onMiss = null,
            Condition? lands = null)
        {
            Id = id;
            Primitive = primitive;
            Attribute = attribute;
            Skill = skill;
            UseGear = useGear;
            Against = against;
            AgainstDefense = againstDefense;
            Magnitude = magnitude;
            FixedMagnitude = fixedMagnitude;
            Cost = cost;
            Target = target;
            OnHit = onHit ?? Array.Empty<Effect>();
            OnMiss = onMiss ?? Array.Empty<Effect>();
            Lands = lands;
        }

        // scoped by the pack; the engine's own two are un-prefixed
        public string Id { get; }

        public Primitive Primitive { get; }


        public Attr Attribute { get; }

        public Skill Skill { get; }

        public bool UseGear { get; }

        public int Against { get; }

        public bool AgainstDefense { get; }


        public Magnitude Magnitude { get; }

        public int FixedMagnitude { get; }

        public Cost Cost { get; }

        public Target Target { get; }

        // lists because the shipped door needs several at once (open, wreckage, recoil)
        public IReadOnlyList<Effect> OnHit { get; }

        public IReadOnlyList<Effect> OnMiss { get; }

        // the channel's condition; a check derives its own from the attribute it used
        public Condition? Lands { get; }


        public string NameKey => KeyConventions.AbilityName(Id);

        public string DescriptionKey => KeyConventions.AbilityDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;
        }


        // exactly Actor.BuildPool; there is no second pool path
        public Pool PoolFor(Actor actor) =>
            actor?.BuildPool(Attribute, Skill, UseGear) ?? new Pool();

        // a presentation filter, not a rule; an ability stuck on the d4 fallback has no power to offer
        public bool Offered(Actor actor)
        {
            if (actor == null) return false;

            if (!actor.Attribute(Attribute).IsReal()) return false;

            // a fixed magnitude needs no spare die
            if (Magnitude == Magnitude.Fixed) return true;

            return Skill == Skill.None || actor.SkillDie(Skill).IsReal();
        }

        // paid as the dice leave the hand; false when it couldn't be paid (empty Nerve), and then nothing is thrown
        public bool Pay(Actor actor)
        {
            if (actor == null) return false;

            switch (Cost)
            {
                case Cost.Strain:
                    Core.Combat.Channeling.Strains(actor);
                    return true;

                case Cost.Nerve:
                    return actor.SpendNerve();

                default:
                    return true;
            }
        }

        public bool Affordable(Actor actor) =>
            actor != null && (Cost != Cost.Nerve || actor.Nerve > 0);

        // targetDefense is ignored unless the ability asked for it, so a caller passing one can't change a fixed difficulty
        public int Difficulty(int targetDefense) => AgainstDefense ? targetDefense : Against;


        // rolls nothing and changes nothing; the magnitude is the die already on the felt, applying effects is the caller's
        public AbilityOutcome Read(PoolResult result, int impact, int targetDefense = 0)
        {
            int against = Difficulty(targetDefense);

            if (result == null) return new AbilityOutcome(this, false, 0, against, 0, OnMiss);

            bool landed = result.Beats(against);

            int magnitude = Magnitude == Magnitude.Fixed ? FixedMagnitude : impact;

            return new AbilityOutcome(this, landed, result.Total, against, magnitude,
                                      landed ? OnHit : OnMiss);
        }

        public override string ToString()
        {
            string versus = AgainstDefense ? "target defense" : Against.ToString();
            string power = Magnitude == Magnitude.Fixed ? FixedMagnitude.ToString() : "impact";

            return $"{Id} [{Primitive}] {Attribute} + {Skill}" + (UseGear ? " + gear" : "") +
                   $" vs {versus}, {power}, {Target}" +
                   (Cost == Cost.None ? "" : $", costs {Cost}") +
                   (OnHit.Count > 0 ? $", hit: {string.Join("+", OnHit)}" : "") +
                   (OnMiss.Count > 0 ? $", miss: {string.Join("+", OnMiss)}" : "") +
                   (Lands != null ? $" ({Lands})" : "");
        }
    }

    // says what happened, not what to do about it; turning effects into board or fight changes is the caller's
    public sealed class AbilityOutcome
    {
        public AbilityOutcome(Ability ability, bool landed, int total, int against, int magnitude,
                              IReadOnlyList<Effect> effects)
        {
            Ability = ability;
            Landed = landed;
            Total = total;
            Against = against;
            Magnitude = magnitude;
            Effects = effects ?? Array.Empty<Effect>();
        }

        public Ability Ability { get; }

        public bool Landed { get; }

        public int Total { get; }

        public int Against { get; }

        // the Impact die or the ability's fixed number; 0 when nothing reads it
        public int Magnitude { get; }

        // empty is an ordinary answer, not a failure
        public IReadOnlyList<Effect> Effects { get; }

        public bool Does(Effect effect) => Effects.Contains(effect);

        // a channel's condition goes on the target; a check's goes on the thrower, derived from the attribute it used
        public Condition? Condition =>
            !Does(Kits.Effect.Condition) ? null
          : Ability.Primitive == Primitive.Channel ? Ability.Lands
          : Ability.Attribute.Pressing();

        public override string ToString() =>
            $"{Ability.Id} {(Landed ? "landed" : "missed")}, {Total} vs {Against}" +
            (Magnitude > 0 ? $", magnitude {Magnitude}" : "") +
            (Effects.Count > 0 ? $" - {string.Join(", ", Effects)}" : " - nothing");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;
using Core.Characters;
using Core.Dice;
using Core.Localization;

namespace Content.Classes
{
    public sealed class ClassCard
    {
        public ClassCard(
            string id,
            int vigor,
            int defense,
            IReadOnlyDictionary<Attr, Die> attributes,
            IReadOnlyDictionary<Skill, Die> skills,
            string wields = null,
            string wears = null,
            IReadOnlyList<string> kit = null,
            string miniId = null,
            int nerveCap = 0,
            IReadOnlyList<Growth> growth = null,
            string companion = null)
        {
            Id = id;
            Vigor = vigor;
            Defense = defense;
            Attributes = attributes ?? new Dictionary<Attr, Die>();
            Skills = skills ?? new Dictionary<Skill, Die>();
            Wields = wields;
            Wears = wears;
            Kit = kit ?? Array.Empty<string>();
            MiniId = miniId ?? "";
            NerveCap = nerveCap;
            Growth = growth ?? Array.Empty<Growth>();
            Companion = companion ?? "";
        }

        // scoped by the pack, so two strangers' Wardens don't collide
        public string Id { get; }

        public int Vigor { get; }

        public int Defense { get; }

        public IReadOnlyDictionary<Attr, Die> Attributes { get; }

        public IReadOnlyDictionary<Skill, Die> Skills { get; }

        // item id, or null for empty-handed (a real class); gear ids are a shared namespace
        public string Wields { get; }

        public string Wears { get; }

        public IReadOnlyList<string> Kit { get; }

        // empty lets the board pick a figure by tier, as it does for a monster
        public string MiniId { get; }

        // 0 means whatever the tier gives (TierRules.NerveCap)
        public int NerveCap { get; }

        // the vocabulary, not the schedule; a campaign says when a step is earned
        public IReadOnlyList<Growth> Growth { get; }

        // "your class isn't a build, it's a relationship" (CORE_RULES.md section 12). The class
        // picks the companion and the companion owns the voice; empty means this class plays alone,
        // which is legal and is what every class did before Phase W.
        public string Companion { get; }

        public bool HasACompanion => Companion.Length > 0;

        public Growth Step(string id) =>
            id == null ? null : Growth.FirstOrDefault(g => g.Id == id);


        public string NameKey => KeyConventions.ClassName(Id);

        public string DescriptionKey => KeyConventions.ClassDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;

            // the figure's own name too; derived, not a field, but the locale audit still has to demand it
            yield return KeyConventions.ActorName(Id);
            yield return KeyConventions.ActorNameNumbered(Id);
        }


        // always a new actor (they're mutable); growth applied after the base dice, through the pipeline, so a save can't drift
        public Actor Create(ItemCatalogue items = null, IEnumerable<string> grown = null)
        {
            var actor = new Actor(Id, Vigor, Defense);

            foreach (KeyValuePair<Attr, Die> a in Attributes) actor.With(a.Key, a.Value);
            foreach (KeyValuePair<Skill, Die> s in Skills) actor.With(s.Key, s.Value);

            Gear held = Find(items, Wields);
            Gear worn = Find(items, Wears);

            if (held != null) actor.Wielding(held);
            if (worn != null) actor.Wearing(worn);

            foreach (string id in grown ?? Array.Empty<string>()) Step(id)?.ApplyTo(actor);

            if (NerveCap > 0)
            {
                actor.NerveCap = NerveCap;

                // a class that lowered the cap below the standing three would otherwise start over its own cap
                actor.RestoreNerve(Math.Min(Core.Combat.Nerve.StartOfDay, NerveCap));
            }

            return actor;
        }

        static Gear Find(ItemCatalogue items, string id) =>
            id == null || items == null ? null : items.Of(id);

        // the half of growth the Actor can't carry; the rules have no idea what a kit is
        public IReadOnlyList<string> KitAfter(IEnumerable<string> grown)
        {
            var kit = new List<string>(Kit);

            foreach (string id in grown ?? Array.Empty<string>())
            {
                string ability = Step(id)?.Ability;

                if (ability != null && !kit.Contains(ability, StringComparer.Ordinal))
                    kit.Add(ability);
            }

            return kit;
        }

        public override string ToString() =>
            $"{Id} vigor {Vigor} def {Defense}, {Attributes.Count} attributes, " +
            $"{Skills.Count} skills, {Wields ?? "empty-handed"}" +
            (Wears != null ? $" over {Wears}" : "") +
            (Kit.Count > 0 ? $", kit: {string.Join(", ", Kit)}" : ", no kit") +
            (MiniId.Length > 0 ? $", stands as {MiniId}" : "") +
            (Companion.Length > 0 ? $", with {Companion}" : "") +
            (Growth.Count > 0 ? $", {Growth.Count} growth steps" : "");
    }
}

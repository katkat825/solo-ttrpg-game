using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Classes;
using Content.Items;
using Core.Characters;
using Core.Combat;
using Core.Dice;

namespace Content.Sheet
{
    // THE SHEET IS THE CHARACTER (R0, THE_TABLE.md section 5).
    //
    // Not a UI representation of the character - the character. The hero Actor is ASSEMBLED FROM
    // THIS, every time, and there is no second source of truth anywhere: no character screen
    // holding its own copy, no HUD number that could drift, no hidden stat block the sheet is a
    // view of. Everything that happens in play is written here, and the dice are re-derived.
    //
    // Which is also why Assemble applies race and background through F3's trait pipeline rather
    // than by setting dice: a value on the sheet and the die in the pool have to be the same fact
    // after a Condition, a growth step and a cursed ring have all landed in some order.
    //
    // Godot-free, so the thing the whole game hangs off can be tested and saved without a table.
    public sealed class CharacterSheet
    {
        // what a real sheet has a blank for and the rules have no opinion about
        public string Name { get; set; } = "";

        public string Appearance { get; set; } = "";

        // pack-scoped content ids; a sheet with no class is a sheet that has not been filled in
        public string ClassId { get; set; } = "";

        public string RaceId { get; set; } = "";

        public string BackgroundId { get; set; } = "";

        // step ids, never the dice they produced, so retuning a class retunes the sheets made
        // against it. The same rule P6 already applies to a save.
        public IList<string> Growth { get; } = new List<string>();

        // item ids; empty means whatever the class card handed over
        public string Wielded { get; set; } = "";

        public string Worn { get; set; } = "";

        public IList<string> Satchel { get; } = new List<string>();


        // ---- what play writes on it -------------------------------------------------------

        // -1 is "as the class made it", which is how a full-Vigor sheet is written down
        public int Vigor { get; set; } = -1;

        public int Nerve { get; set; } = Core.Combat.Nerve.StartOfDay;

        public IList<Condition> Conditions { get; } = new List<Condition>();

        public int Strain { get; set; }

        public int Notches { get; set; }

        // how lived-in the paper looks: every erasure and rewrite, counted (R2). Not a rule and
        // not a stat - it is the record of play, and the record of play IS the artifact
        public int Erasures { get; set; }

        public bool IsBlank => ClassId.Length == 0;


        public IEnumerable<string> Filled()
        {
            if (ClassId.Length > 0) yield return ClassId;
            if (RaceId.Length > 0) yield return RaceId;
            if (BackgroundId.Length > 0) yield return BackgroundId;
        }

        // ---- the character -----------------------------------------------------------------

        // Always a new Actor: actors are mutable, and a shared hero carries the last fight's
        // wounds. Order matters exactly once - the class makes the dice, then everything else
        // nudges them through the pipeline, so nothing here can un-write something above it.
        public Actor Assemble(ClassRoster classes, ItemCatalogue items = null,
                              SheetOptions options = null)
        {
            ClassCard card = classes?.Of(ClassId);

            if (card == null)
                throw new InvalidOperationException(
                    $"This sheet says its class is '{ClassId}' and no such class is installed. " +
                    "A sheet with no class is a sheet nobody has filled in yet.");

            Actor hero = card.Create(items, Growth);

            foreach (TraitCard trait in Cards(options)) trait.ApplyTo(hero);

            Equip(hero, items);

            Restore(hero);

            return hero;
        }

        // race then background, always in that order, so a transcript reads the same twice - the
        // modifiers themselves are steps and compose in any order, but the printout should not shuffle
        public IEnumerable<TraitCard> Cards(SheetOptions options)
        {
            if (options == null) yield break;

            if (options.Of(RaceId) is { } race) yield return race;

            if (options.Of(BackgroundId) is { } background) yield return background;
        }

        void Equip(Actor hero, ItemCatalogue items)
        {
            if (items == null) return;

            if (Wielded.Length > 0 && items.Of(Wielded) is { } held) hero.Wielding(held);

            if (Worn.Length > 0 && items.Of(Worn) is { } worn) hero.Wearing(worn);

            foreach (string id in Satchel) hero.Carry(id);
        }

        // the marks the DM made in play, put back on a freshly made hero
        void Restore(Actor hero)
        {
            for (int at = 0; at < Notches; at++) hero.NotchGear();

            if (Strain > 0) hero.AddStrain(Strain);

            foreach (Condition condition in Conditions) hero.ApplyCondition(condition);

            if (Vigor >= 0) hero.RestoreVigor(Vigor);

            hero.RestoreNerve(Nerve);
        }


        // ---- what play writes back ---------------------------------------------------------

        // The sheet is written FROM the actor after play, because the actor is what the rules
        // touched. Erasures counts the rewrites rather than the changes: a number the DM had to
        // rub out is a smudge on the paper, and a number that stayed put is not.
        public CharacterSheet Wrote(Actor hero)
        {
            if (hero == null) return this;

            Erasures += Rubbings(hero);

            Vigor = hero.Vigor;
            Nerve = hero.Nerve;
            Strain = hero.Strain;
            Notches = hero.Notches;

            Conditions.Clear();

            foreach (Condition condition in hero.Conditions) Conditions.Add(condition);

            Satchel.Clear();

            foreach (string id in hero.Satchel) Satchel.Add(id);

            return this;
        }

        int Rubbings(Actor hero)
        {
            int rubbed = 0;

            if (Vigor >= 0 && hero.Vigor != Vigor) rubbed++;
            if (hero.Nerve != Nerve) rubbed++;
            if (hero.Strain != Strain) rubbed++;
            if (hero.Notches != Notches) rubbed++;
            if (!hero.Conditions.SequenceEqual(Conditions)) rubbed++;

            return rubbed;
        }

        // a growth step earned in play; the die is never written down, only the step that bought it
        public bool Grew(string step)
        {
            if (string.IsNullOrEmpty(step) || Growth.Contains(step, StringComparer.Ordinal))
                return false;

            Growth.Add(step);
            Erasures++;

            return true;
        }

        public CharacterSheet Copy()
        {
            var copy = new CharacterSheet
            {
                Name = Name,
                Appearance = Appearance,
                ClassId = ClassId,
                RaceId = RaceId,
                BackgroundId = BackgroundId,
                Wielded = Wielded,
                Worn = Worn,
                Vigor = Vigor,
                Nerve = Nerve,
                Strain = Strain,
                Notches = Notches,
                Erasures = Erasures,
            };

            foreach (string step in Growth) copy.Growth.Add(step);
            foreach (string id in Satchel) copy.Satchel.Add(id);
            foreach (Condition condition in Conditions) copy.Conditions.Add(condition);

            return copy;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            IsBlank
                ? "a blank sheet"
                : $"{(Name.Length > 0 ? Name : "unnamed")}, " +
                  $"{string.Join(" ", Filled().Select(ContentId.LocalOf))}" +
                  (Vigor >= 0 ? $", vigor {Vigor}" : "") +
                  $", nerve {Nerve}" +
                  (Conditions.Count > 0 ? ", " + string.Join(", ", Conditions) : "") +
                  (Growth.Count > 0 ? $", {Growth.Count} grown" : "") +
                  (Erasures > 0 ? $", {Erasures} erasures" : "");
    }
}

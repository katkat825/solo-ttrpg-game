using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Localization;

namespace Content.Sheet
{
    // What a blank on the character sheet offers. A race and a background are the same shape - a
    // named bundle that nudges a die or two - so they are one type with a Blank saying which line
    // of the sheet it fills.
    //
    // ON THE NAMESPACE. These are keyed under 'class' (class.hearthguard.hillfolk.name), which is
    // the one part of this that is worth a paragraph. The key namespaces are a CLOSED set
    // (CONVENTIONS.md section 7) and a new one is a design decision rather than a convenience, so
    // the question was which existing one is honest. 'ui' is the engine's chrome and would put a
    // pack author's strings in the base game's bucket - the exact mistake 'mini' was added to
    // avoid. 'actor' would say a race is a statblock, which it is not. 'class' was opened in Phase
    // K for "a data bundle that says what kind of character this is", and a race and a background
    // are more of exactly that: the sheet's other blanks, filled from the same kind of card.
    //
    // The ids are pack-scoped like everything else, and a race sharing an id with a class in the
    // same pack is refused at load - they would otherwise collide in that namespace.
    public sealed class TraitCard
    {
        public TraitCard(string id, Blank blank,
                         IReadOnlyDictionary<Attr, int> nudges = null,
                         IReadOnlyDictionary<Skill, Die> skills = null)
        {
            Id = id;
            Fills = blank;
            Nudges = nudges ?? new Dictionary<Attr, int>();
            Skills = skills ?? new Dictionary<Skill, Core.Dice.Die>();
        }

        public string Id { get; }

        public Blank Fills { get; }

        // STEPS, never die sizes, so a race and a background and a Condition compose in any order.
        // This is F3's trait pipeline and it is the whole reason writing on the sheet is safe.
        public IReadOnlyDictionary<Attr, int> Nudges { get; }

        // skills are not on the pipeline, so a card trains one rather than modifying it
        public IReadOnlyDictionary<Skill, Die> Skills { get; }

        // prefixed, because the modifier id space is shared with growth, strain and phase changes
        public ModifierSource Source => ModifierSource.Effect(Fills.Word() + ":" + Id);

        public string NameKey => KeyConventions.ClassName(Id);

        public string DescriptionKey => KeyConventions.ClassDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;
        }

        // through the pipeline, so what is written on the sheet and what is in the pool are one fact
        public void ApplyTo(Actor actor)
        {
            if (actor == null) return;

            foreach (KeyValuePair<Attr, int> nudge in Nudges)
                actor.AddModifier(new TraitModifier(Source, nudge.Key, nudge.Value));

            foreach (KeyValuePair<Skill, Die> trained in Skills)
            {
                Die had = actor.SkillDie(trained.Key);

                // the better of the two: a background that trains Blades d4 must not un-train a
                // class that already had Blades d6
                actor.With(trained.Key, (int)trained.Value > (int)had ? trained.Value : had);
            }
        }

        public void TakeFrom(Actor actor) => actor?.RemoveModifiers(Source);

        public override string ToString() =>
            $"{Id} [{Fills.Word()}]" +
            (Nudges.Count > 0
                ? ", " + string.Join(", ", Nudges.Select(n => $"{n.Key} {n.Value:+0;-0}"))
                : "") +
            (Skills.Count > 0
                ? ", " + string.Join(", ", Skills.Select(s => $"{s.Key} {s.Value.Label()}"))
                : "");
    }

    // which line of the sheet a card fills. Closed: a sheet has the blanks it has, and a new one
    // is a new line drawn on the paper, not a data file
    public enum Blank
    {
        Race,

        Background,
    }

    public static class Blanks
    {
        public static string Word(this Blank blank) => blank.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words => Enum.GetValues<Blank>().Select(Word).ToArray();

        // the folder each kind lives in, plural, matching every other content folder
        public static string Folder(this Blank blank) => blank.Word() + "s";

        public static bool TryWord(string word, out Blank blank)
        {
            blank = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim();

            if (trimmed.Any(char.IsDigit)) return false;

            return Enum.TryParse(trimmed, ignoreCase: true, out blank)
                && Enum.IsDefined(typeof(Blank), blank);
        }
    }
}

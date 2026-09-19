using System;
using System.Collections.Generic;
using System.Linq;
using Content.Kits;
using Core.Characters;
using Core.Localization;
using Core.Resolution;

namespace Content.Sheet
{
    // THE CHECKS YOU REACH FOR YOURSELF.
    //
    // A verb the moment offers is the moment's - the author permitted it, and it arrives on a card
    // beside the thing it is about. These are the other half: the things you decide to try, which
    // nobody offered you. They belong on the character sheet for that reason and no other, because
    // the sheet is the one object at the table that is yours.
    //
    // NONE OF THEM IS A NEW RULE. Each is the check primitive the kit already runs - an attribute
    // and a skill against a difficulty - so a scene allowing one is a number in a campaign file
    // and never code. That is also why the difficulty is an argument rather than a field: the
    // scene sets it, and the same intimidation is trivial in a tavern and formidable at a gate.
    public enum Check
    {
        Intimidate,

        Persuade,

        Perception,
    }

    public static class Checks
    {
        public static IReadOnlyList<Check> All { get; } = Enum.GetValues<Check>();

        public static string Word(this Check check) => check.ToString().ToLowerInvariant();

        // leaning on somebody is Might, winning them over is Heart, and both are the same skill:
        // Sway is what you are trained in, and which attribute carries it is what differs
        public static Attr Attribute(this Check check) => check switch
        {
            Check.Intimidate => Attr.Might,
            Check.Persuade => Attr.Heart,
            _ => Attr.Wits,
        };

        public static Skill Skill(this Check check) => check switch
        {
            Check.Perception => Core.Characters.Skill.Insight,
            _ => Core.Characters.Skill.Sway,
        };

        // NO GEAR. An axe does not help you read a room, and a check that counted the weapon in
        // one hand would make the sheet's three a question about your inventory
        public static Ability For(this Check check, int against = Difficulty.Standard) =>
            new Ability(
                check.Word(),
                Primitive.Check,
                check.Attribute(),
                check.Skill(),
                useGear: false,
                against: against,
                magnitude: Magnitude.Fixed,
                cost: Cost.None,
                target: Target.Sight);

        public static string NameKey(this Check check) => KeyConventions.AbilityName(check.Word());

        public static string DescriptionKey(this Check check) =>
            KeyConventions.AbilityDescription(check.Word());

        public static IEnumerable<string> Keys()
        {
            foreach (Check check in All)
            {
                yield return check.NameKey();
                yield return check.DescriptionKey();
            }
        }

        public static bool TryWord(string word, out Check check)
        {
            check = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Check one in All)
            {
                if (one.Word() != trimmed) continue;

                check = one;
                return true;
            }

            return false;
        }

        public static IReadOnlyList<string> Words => All.Select(Word).ToArray();
    }
}

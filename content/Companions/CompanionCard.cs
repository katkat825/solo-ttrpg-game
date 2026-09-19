using System;
using System.Collections.Generic;

namespace Content.Companions
{
    // A companion is content and weightlessness both (COMPANION_AND_DIALOGUE.md, architecture note).
    //
    // There is deliberately NO Vigor, no Defense, no attributes, no skills, no tier and no square on
    // this card, and there must never be. CORE_RULES.md section 13 lists a companion with its own
    // turn among the things written down so they stay dead; the shape of this type is where that
    // stays true, because a thing with no numbers cannot be handed to CombatEngine by accident.
    public sealed class CompanionCard
    {
        // a dozen idles and a handful of reactions read as a living creature (THE_TABLE.md section 4)
        public const int IdlesByDefault = 12;

        public const int MostIdles = 64;

        // wolf-sized, because the wolf is the one the table was built around
        public const float NormalSize = 1f;

        public const float Smallest = 0.2f;

        public const float Largest = 3f;

        public CompanionCard(string id, Perch perch, string voice = null, string mini = null,
                             int idles = IdlesByDefault, float size = NormalSize)
        {
            Id = id;
            Perch = perch;
            Voice = string.IsNullOrEmpty(voice) ? id : voice;
            Mini = mini ?? "";
            Idles = idles;
            Size = size;
        }

        // pack-scoped, like every other content id, so two strangers' ravens do not collide
        public string Id { get; }

        public Perch Perch { get; }

        // the creature whose bark bank it speaks out of - 'wolf', not 'barbarian'. Usually its own
        // id without the pack on the front, because a voice belongs to the base game and an id does not.
        public string Voice { get; }

        // empty means the placeholder shape; a mini id paints a real figure the way a monster's does
        public string Mini { get; }

        public int Idles { get; }

        // HOW BIG IT IS, because a wolf, a raven and a reliquary are not the same creature at the
        // same scale. It is identity and not decoration - a raven rendered wolf-sized is a
        // different animal - and it is still nothing a rule ever reads: there is no Vigor on this
        // card and a big companion is not a strong one.
        public float Size { get; }

        public override string ToString() =>
            $"{Id} on the {Perch.Word()}, speaks as {Voice}" +
            (Mini.Length > 0 ? $", stands as {Mini}" : "") +
            $", {Idles} idles" +
            (Size == NormalSize ? "" : $", {Size:0.##} size");
    }
}

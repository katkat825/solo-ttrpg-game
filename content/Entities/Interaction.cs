using System;
using System.Linq;
using Content.World;

namespace Content.Entities
{
    public enum Interaction
    {
        Talk,

        Attack,

        Examine,

        Open,

        Search,
    }

    public static class Interactions
    {
        public static string Word(this Interaction verb) => verb.ToString().ToLowerInvariant();

        public static bool Has(string word) =>
            word != null && Enum.GetNames<Interaction>()
                                .Any(n => string.Equals(n, word, StringComparison.OrdinalIgnoreCase));

        // what the author writes beside the verb; "" where the verb needs nothing said
        public static string Wants(this Interaction verb) => verb switch
        {
            Interaction.Talk => "the id of a node in this campaign's dialogue/ folder",
            Interaction.Examine => "the id of the line the DM reads, as a cue does",
            _ => "",
        };

        public static bool NeedsAName(this Interaction verb) => verb.Wants().Length > 0;

        // attacking a thing means fighting it, so it needs a statblock to fight as
        public static bool NeedsAStatblock(this Interaction verb) => verb == Interaction.Attack;

        public static bool NeedsLoot(this Interaction verb) => verb == Interaction.Search;

        // the fact the verb writes, derived; an author's 'bob.spoken' matches the one the engine set
        public static string Writes(this Interaction verb, string entity) => verb switch
        {
            Interaction.Talk => FactName.Spoken(entity),
            Interaction.Attack => FactName.Dead(entity),
            Interaction.Open => FactName.Open(entity),
            Interaction.Search => FactName.Looted(entity),
            _ => "",
        };

        // what takes a verb off the menu; talking and looking are the two you may repeat
        public static bool Repeatable(this Interaction verb) =>
            verb is Interaction.Talk or Interaction.Examine;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Access
{
    // EVERY VISUAL CUE IN THE GAME THAT MEANS SOMETHING (AX3).
    //
    // "No information in colour alone" is a standing rule, and a standing rule with nothing holding it
    // is a rule that lasts until the first awkward milestone. The trouble is that colour redundancy is
    // not mechanically checkable the way a missing string is: nothing can look at a ring round a die
    // and decide whether a colourblind player can read it.
    //
    // What IS checkable is whether anybody has answered the question. So this is the register: one
    // member per cue the game draws to mean something, and for each one the TWIN that carries the same
    // meaning without colour. A cue with no twin fails the check, which makes the sweep AX3 asks for a
    // thing that happens once and then keeps happening - a milestone that adds a coloured indicator
    // and no twin cannot pass.
    //
    // The twins are mostly already there, and that is the point worth recording: the walls and doors
    // on the board were built "told apart by silhouette" long before anybody wrote this down, the
    // conditions on a piece were always words, and a foe's health has been a descriptor rather than a
    // bar since the first fight. What this adds is that none of it can quietly stop being true.
    public enum Cue
    {
        // the ring round a die that counted toward the total
        DieCounted,

        // the Impact die: the one that did not count and is thrown again for damage
        DieImpact,

        // a die that neither counted nor is the Impact
        DieSpare,

        // exactly one 1, which is cosmetic and is the companion's cue to speak
        Snag,

        // a condition on a piece
        Condition,

        // a foe's health, coarse on purpose
        FoeHealth,

        // the hero's Vigor, as a tally beside the piece
        HeroVigor,

        // Nerve left, as chips
        Nerve,

        // a Nerve chip committed to a re-throw
        NerveSpent,

        // floor, wall, door, rough ground, rock
        MapTile,

        // a door forced or given way
        DoorOpen,

        // the square something just happened on
        Flash,
    }

    // WHAT CARRIES THE MEANING WHEN THE COLOUR DOES NOT.
    public enum Twin
    {
        // nothing does, which is a bug
        None,

        // a word printed beside it, in the locale
        Label,

        // a figure printed somewhere the player is already looking
        Number,

        // a different silhouette, size or thickness
        Shape,

        // a different position or height: standing up, swung open, lifted
        Standing,

        // somebody says something
        Words,
    }

    public static class Redundancies
    {
        public static string Word(this Cue cue) => cue switch
        {
            Cue.DieCounted => "die_counted",
            Cue.DieImpact => "die_impact",
            Cue.DieSpare => "die_spare",
            Cue.FoeHealth => "foe_health",
            Cue.HeroVigor => "hero_vigor",
            Cue.NerveSpent => "nerve_spent",
            Cue.MapTile => "map_tile",
            Cue.DoorOpen => "door_open",
            _ => cue.ToString().ToLowerInvariant(),
        };

        public static IReadOnlyList<string> Words => Enum.GetValues<Cue>().Select(Word).ToArray();

        public static Twin TwinOf(this Cue cue) => cue switch
        {
            Cue.DieCounted => Twin.Label,
            Cue.DieImpact => Twin.Shape,
            Cue.DieSpare => Twin.Shape,
            Cue.Snag => Twin.Words,
            Cue.Condition => Twin.Label,
            Cue.FoeHealth => Twin.Label,
            Cue.HeroVigor => Twin.Number,
            Cue.Nerve => Twin.Number,
            Cue.NerveSpent => Twin.Standing,
            Cue.MapTile => Twin.Shape,
            Cue.DoorOpen => Twin.Standing,
            Cue.Flash => Twin.Standing,
            _ => Twin.None,
        };

        public static bool Carried(this Cue cue) => TwinOf(cue) != Twin.None;

        // HOW, IN ONE LINE EACH. Developer-facing and never localized: it is here so the answer is
        // written down beside the question, and so a reader can go and look at the thing named.
        public static string How(this Cue cue) => cue switch
        {
            Cue.DieCounted => "the die's own attribute or skill name is lettered beside it (DieMark)",
            Cue.DieImpact => "a ring twice the thickness of a counted die's, and a breathing halo " +
                             "no other die has (DieMark)",
            Cue.DieSpare => "no ring at all - absence, which reads at any colour vision (DieMark)",
            Cue.Snag => "the companion speaks. The ring is the flourish; the bark is the cue, and it " +
                        "is text (SnagCue, SnagFlash)",
            Cue.Condition => "the condition's name in words, one line per condition (ConditionMarks)",
            Cue.FoeHealth => "the band as a word - unharmed, wounded, badly wounded - printed on the " +
                             "initiative card (Card, CardKeys)",
            Cue.HeroVigor => "\"Vigor 11\" on the hero's own initiative card, and the pips are a " +
                             "countable tally rather than a bar (VigorPips, Card)",
            Cue.Nerve => "\"Nerve 3 of 5\" above the pips on the character sheet (Sheet, SheetKeys)",
            Cue.NerveSpent => "a committed chip stands off the table; a spent one is not there " +
                              "(NerveTokens)",
            Cue.MapTile => "height and silhouette: a wall is tall, a door is the same line lower, " +
                           "rough ground is inset, rock fills its square (BoardTiles)",
            Cue.DoorOpen => "the leaf swings, and a door given way leans off the vertical (DoorPiece)",
            Cue.Flash => "the piece it is about moved there, and the move is what you watch (Mini, " +
                         "MiniStep, CellFlash)",
            _ => "nothing - this cue is carried by colour alone, which is a bug",
        };

        // the whole register, in the enum's order
        public static IEnumerable<KeyValuePair<Cue, Twin>> All
        {
            get
            {
                foreach (Cue cue in Enum.GetValues<Cue>())
                    yield return new KeyValuePair<Cue, Twin>(cue, TwinOf(cue));
            }
        }

        public static IEnumerable<Cue> Bare => Enum.GetValues<Cue>().Where(c => !Carried(c));
    }
}

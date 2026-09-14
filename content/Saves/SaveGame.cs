using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Dice;

namespace Content.Saves
{
    // A SAVED GAME (CONTENT_PIPELINE.md P6, ARCHITECTURE.md section 8).
    //
    // "Versioned, plain-text, hand-editable JSON. No online anything. In 2036 you will want to
    // hand-edit a save." So this is a flat description of a moment - not a serialized object
    // graph, not a snapshot of node fields, and deliberately nothing that would have to be
    // reverse-engineered by whoever opens it in a text editor in ten years.
    //
    // WHAT IS IN IT AND WHAT IS NOT. What is saved is what CANNOT be derived: the hero's Vigor,
    // his Nerve, his Conditions, what he is holding, who else is in the room, where everybody is
    // standing, and the faces lying on the felt. What is NOT saved is everything a campaign
    // already says - a ghoul's Vigor track, a map's walls, an item's die - because those are read
    // back out of the folder by id. A save that copied them would be a second, stale copy of the
    // campaign, and updating a campaign would silently not update the saves made against it.
    //
    // WHICH IS ALSO WHY IT NAMES ITS CAMPAIGN. A save references its campaign by id and content
    // format, and degrades when either changes: unsubscribe from a Workshop item and the save
    // says which one is missing and returns to the shelf, rather than loading a hero standing in
    // an empty room holding a name nothing can resolve.
    //
    // THE FELT IS FACES AND NOTHING ELSE. A throw is saved as the dice and what they are showing;
    // what that throw MEANS is read back with `PoolResult.From`, the same arithmetic the table
    // runs. `SEAMS.md` section 9 named the alternative as the specific obstacle to round tripping
    // - a saved throw carrying both the rolls and the verdict is two fields that can disagree, and
    // there is no honest thing to do when they do.
    public sealed class SaveGame
    {
        // which chapter and encounter, and nothing about how far into it - an encounter is the
        // unit a fight happens in, and a save mid-fight carries the fight itself below
        public string Campaign { get; set; } = "";

        public int CampaignFormat { get; set; }

        public string Chapter { get; set; } = "";

        public string Encounter { get; set; } = "";

        // WHAT BUILT IT. Not a gate - a save from a later build is read as best it can be, because
        // refusing to open somebody's save is a far worse outcome than opening it with a warning -
        // but it is the first thing anybody debugging a strange save wants to know
        public int Format { get; set; } = SaveFormat.Current;

        public Version Engine { get; set; } = Core.EngineVersion.Current;

        // ---- the fight, when there is one ----

        // 0 when the save was not taken mid-fight
        public int Round { get; set; }

        // whose turn it is, as a `SavedActor.Seat`. -1 for a save with no fight in it
        public int Turn { get; set; } = -1;

        public int ActionsLeft { get; set; }

        public SavedActor Hero { get; set; }

        public IList<SavedActor> Foes { get; } = new List<SavedActor>();

        public IList<SavedDie> Felt { get; } = new List<SavedDie>();

        public bool MidFight => Round > 0;

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{Campaign}/{Encounter} format {Format}" +
            (MidFight ? $", round {Round}, {Foes.Count} foes, {Felt.Count} dice on the felt"
                      : ", not mid-fight");
    }

    // ONE ACTOR AS THEY STAND. Ids rather than values throughout - a statblock says what a ghoul
    // IS and this says what has happened to this one
    public sealed class SavedActor
    {
        public string Id { get; set; } = "";

        // -1 for "the archetype's own", which is what a hero at full Vigor saves as
        public int Vigor { get; set; } = -1;

        public int Nerve { get; set; }

        // so "Rabble 3" comes back as Rabble 3 rather than as Rabble 1
        public int Ordinal { get; set; }

        // WHICH SPAWN SLOT THIS ONE STARTED ON, and 0 for the hero or for a foe the scene placed
        // by square. It is how a save names one foe out of four identical ones: an id repeats, a
        // square moves, and the slot the encounter put them on does neither
        public int Slot { get; set; }

        // WHERE THEY COME IN THE TURN ORDER - 0 first, hero and foes counted together, and one
        // seat per actor still standing. It is saved rather than derived because deriving it means
        // re-sorting by the initiative numbers and re-applying a tie-break rule, and a tie-break
        // rule that changed would silently re-order every save ever written (Encounter.BuildOrder
        // breaks ties by muster seat, which is not in the file at all).
        //
        // -1 for a save taken before the fight began
        public int Seat { get; set; } = -1;

        // AND WHAT THEY THREW FOR IT, which is cosmetic - it is the number written down the side
        // of the map - and is saved so a reloaded fight shows the same ones rather than blanks
        public int Initiative { get; set; }

        // how far the weapon has been notched (Actor.NotchGear) and how far Channelling has
        // stepped the Heart die down (Actor.AddStrain). Both are counts rather than die sizes,
        // because the die they act on comes from the statblock and may have changed
        public int Notches { get; set; }

        public int Strain { get; set; }

        public IList<Condition> Conditions { get; } = new List<Condition>();

        // item ids. Empty means "whatever the statblock gave them"
        public string Wielded { get; set; } = "";

        public string Worn { get; set; } = "";

        public IList<string> Satchel { get; } = new List<string>();

        // where the piece is standing, or null for an actor that is not on the board - a fallen
        // one, or a hero between encounters
        public int? X { get; set; }

        public int? Y { get; set; }

        public bool OnTheBoard => X.HasValue && Y.HasValue;

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{Id}" + (Ordinal > 0 ? $"#{Ordinal}" : "") + $" vigor {Vigor}" +
            (OnTheBoard ? $" on ({X}, {Y})" : " off the board");
    }

    // ONE DIE LYING ON THE FELT: which trait threw it, how big it is, and what it is showing.
    // Exactly `Game.Tray.TraySlot`, which is not referenced here because that type is in the Godot
    // project and this assembly must never be
    public sealed class SavedDie
    {
        public SavedDie() { }

        public SavedDie(string trait, Die die, int value)
        {
            Trait = trait ?? "";
            Die = die;
            Value = value;
        }

        // the localization key of the trait that contributed it - `attr.might.name`. A key rather
        // than a word, for the same reason everything else in this project is a key
        public string Trait { get; set; } = "";

        public Die Die { get; set; } = Die.None;

        public int Value { get; set; }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() => $"{Trait} {Die}->{Value}";
    }
}

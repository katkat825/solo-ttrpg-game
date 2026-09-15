using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Dice;

namespace Content.Saves
{
    public sealed class SaveGame
    {
        public string Campaign { get; set; } = "";

        public int CampaignFormat { get; set; }

        public string Chapter { get; set; } = "";

        public string Encounter { get; set; } = "";

        public int Format { get; set; } = SaveFormat.Current;

        public Version Engine { get; set; } = Core.EngineVersion.Current;


        public int Round { get; set; }

        public int Turn { get; set; } = -1;

        public int ActionsLeft { get; set; }

        public SavedActor Hero { get; set; }

        public IList<SavedActor> Foes { get; } = new List<SavedActor>();

        public IList<SavedDie> Felt { get; } = new List<SavedDie>();

        public bool MidFight => Round > 0;

        public override string ToString() =>
            $"{Campaign}/{Encounter} format {Format}" +
            (MidFight ? $", round {Round}, {Foes.Count} foes, {Felt.Count} dice on the felt"
                      : ", not mid-fight");
    }

    // ids, not values: the statblock says what a ghoul is, this says what happened to this one
    public sealed class SavedActor
    {
        public string Id { get; set; } = "";

        // -1 means the archetype's own, which is how a full-Vigor actor saves
        public int Vigor { get; set; } = -1;

        public int Nerve { get; set; }

        public int Ordinal { get; set; }

        // how a save names one of four identical foes; 0 for the hero or a scene-placed foe
        public int Slot { get; set; }

        // stored, not derived: a changed tie-break rule would otherwise silently re-order every save; -1 before the fight began
        public int Seat { get; set; } = -1;

        public int Initiative { get; set; }

        // counts, not die sizes: the die comes from the statblock and may have changed
        public int Notches { get; set; }

        public int Strain { get; set; }

        public IList<Condition> Conditions { get; } = new List<Condition>();

        // item ids; empty means whatever the statblock gave them
        public string Wielded { get; set; } = "";

        public string Worn { get; set; } = "";

        public IList<string> Satchel { get; } = new List<string>();

        // step ids, never the dice they produced, so retuning a class retunes the saves made against it
        public IList<string> Growth { get; } = new List<string>();

        // null for an actor not on the board (fallen, or a hero between encounters)
        public int? X { get; set; }

        public int? Y { get; set; }

        public bool OnTheBoard => X.HasValue && Y.HasValue;

        public override string ToString() =>
            $"{Id}" + (Ordinal > 0 ? $"#{Ordinal}" : "") + $" vigor {Vigor}" +
            (OnTheBoard ? $" on ({X}, {Y})" : " off the board");
    }

    // exactly Game.Tray.TraySlot; not referenced, because that type is in the Godot project and this assembly must never be
    public sealed class SavedDie
    {
        public SavedDie() { }

        public SavedDie(string trait, Die die, int value)
        {
            Trait = trait ?? "";
            Die = die;
            Value = value;
        }

        public string Trait { get; set; } = "";

        public Die Die { get; set; } = Die.None;

        public int Value { get; set; }

        public override string ToString() => $"{Trait} {Die}->{Value}";
    }
}

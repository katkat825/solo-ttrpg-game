using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Localization;

namespace Game.Fight
{
    // The words printed on an initiative card. Engine strings under ui.*, like the ones printed on
    // the character sheet: every campaign is fought on the same card, and a foe is unharmed in all
    // of them. What goes in a card's name line is the actor's own key and belongs to whoever ships
    // the actor.
    //
    // Derived from the Health enum rather than listed, so a fourth band would fail the locale audit
    // until somebody wrote the word for it.
    public static class CardKeys
    {
        public const string Subject = "initiative_card";

        // "Vigor 11", on the hero's own card only - a number you can glance at instead of
        // stopping to read the sheet
        public static string Vigor => KeyConventions.Key(KeyConventions.UiNs, Subject, "vigor");

        // "Defence 13", on a foe's card once you have beaten it
        public static string Defence => KeyConventions.Key(KeyConventions.UiNs, Subject, "defence");

        // a foe's health, as a word and never as a number
        public static string Band(Health band) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, "health", band.Word());

        public static IEnumerable<string> All()
        {
            yield return Vigor;
            yield return Defence;

            foreach (Health band in Enum.GetValues<Health>()) yield return Band(band);
        }

        // the two that count a number in; a band is a word and would print braces if it had one
        public static bool TakesAnArgument(string key) => key == Vigor || key == Defence;
    }
}

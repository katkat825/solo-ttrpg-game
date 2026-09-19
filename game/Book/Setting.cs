using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Game.Access;

namespace Game.Book
{
    // WHAT IS ON THE SETTINGS PAGE.
    //
    // The one honestly-meta page in the book, and it stays honest: a plain page in a book is wrapper
    // enough, and fictionalising volume and text size would make them harder to find without making
    // them any more diegetic.
    //
    // THE DIALS ARE REAL NOW (Phase AX). This page was printed with its headings and nothing turnable
    // on purpose, because the dials behind it - resizable text, high contrast, speech speed, captions
    // - were the accessibility build and retrofitting them would have been a rewrite. That build
    // happened, so each line below turns, and what it turns is an Adjustments: this page knows how to
    // print a line and cycle it, and nothing else. The meanings live with the dials.
    //
    // ACCESSIBILITY IS NO LONGER A LINE ON THIS PAGE, and that is the change worth recording. It was
    // one, as a placeholder standing for the whole build - and now that each dial is its own line,
    // keeping it would be a heading over nothing, which is exactly the second description of the same
    // thing this project refuses everywhere else.
    //
    // VOLUME IS THE ONE THAT IS STILL NOT TURNABLE, and Built() says so rather than a comment: it is
    // an audio-options task and not an accessibility one, the game's only recordings today are dice on
    // wood, and a dial over nothing is worse than a line that plainly does not turn yet.
    public enum Setting
    {
        Volume,

        TextSize,

        Contrast,

        // how long a line of speech stays up, which in a game with no voices IS the speech speed
        Speech,

        Captions,

        // the screen reader
        ReadAloud,

        // your own arms, and what colour they are
        Arms,

        // two, left, right or hidden
        ArmsShown,
    }

    public static class Settings
    {
        public static string Word(this Setting setting) => setting switch
        {
            Setting.TextSize => "text_size",
            Setting.ReadAloud => "read_aloud",
            Setting.ArmsShown => "arms_shown",
            _ => setting.ToString().ToLowerInvariant(),
        };

        public static IReadOnlyList<string> Words =>
            Enum.GetValues<Setting>().Select(Word).ToArray();

        public const string Subject = "setting";

        // "Text size - {0}", with the value counted in. One key per whole sentence, the way an actor's
        // numbered name is: a page of "Text size" and "Large" in two columns cannot be translated into
        // a language that puts them the other way round
        public static string NameKey(this Setting setting) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(setting), "name");

        // ui.setting.text_size.large - the value's own word, in the player's language. Each setting
        // keeps its own, even where two of them say "on": a key shared between two dials is a key that
        // cannot be translated differently when one of them needs it to be
        public static string ValueKey(this Setting setting, string value) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(setting), value);

        // THE VALUES EACH LINE CYCLES THROUGH, asked of the dials rather than listed here. A dial that
        // grows a step grows this page with it, and the locale audit fails until the step has a word.
        public static IReadOnlyList<string> Choices(this Setting setting) => setting switch
        {
            Setting.TextSize => Enum.GetValues<Lettering>()
                                    .Select(l => l.ToString().ToLowerInvariant()).ToArray(),

            Setting.Speech => Enum.GetValues<Pace>()
                                  .Select(p => p.ToString().ToLowerInvariant()).ToArray(),

            Setting.Contrast or Setting.Captions or Setting.ReadAloud =>
                new[] { Adjustments.Off, Adjustments.On },

            Setting.Arms => Access.Arms.Words,

            Setting.ArmsShown => Access.Arms.Showings,

            _ => Array.Empty<string>(),
        };

        // derived, so a dial that gains its values gains its line in the same edit
        public static bool Built(this Setting setting) => Choices(setting).Count > 0;

        public static IEnumerable<string> Keys()
        {
            foreach (Setting setting in Enum.GetValues<Setting>())
            {
                yield return NameKey(setting);

                foreach (string value in Choices(setting)) yield return ValueKey(setting, value);
            }
        }

        // every built line counts its value in. Volume has nothing to count, and the day it does this
        // stops excusing it
        public static bool TakesAnArgument(string key) =>
            Enum.GetValues<Setting>().Any(s => Built(s) && NameKey(s) == key);


        // ---- reading and turning one -----------------------------------------------------------

        // WHERE THIS LINE IS SET, as the value's own word
        public static string Value(this Setting setting, Adjustments how)
        {
            if (how == null) return "";

            return setting switch
            {
                Setting.TextSize => how.Lettering.ToString().ToLowerInvariant(),
                Setting.Speech => how.Speech.ToString().ToLowerInvariant(),
                Setting.Contrast => Said(how.HighContrast),
                Setting.Captions => Said(how.Captions),
                Setting.ReadAloud => Said(how.ReadAloud),
                Setting.Arms => how.Skin.Word(),
                Setting.ArmsShown => how.Showing.Word(),
                _ => "",
            };
        }

        static string Said(bool on) => on ? Adjustments.On : Adjustments.Off;

        // TOUCHED, SO IT GOES ROUND ONE. Cycling rather than a slider is the same call the character
        // sheet's blanks make: a thing you touch and it changes, with no handle to drag and nothing to
        // confirm. Returns whether anything moved, so the page can say nothing happened.
        public static bool Turn(this Setting setting, Adjustments how)
        {
            if (how == null || !Built(setting)) return false;

            IReadOnlyList<string> choices = Choices(setting);

            int at = Math.Max(0, choices.ToList().IndexOf(Value(setting, how)));

            return Set(setting, how, choices[(at + 1) % choices.Count]);
        }

        public static bool Set(this Setting setting, Adjustments how, string value)
        {
            if (how == null || string.IsNullOrWhiteSpace(value)) return false;

            string word = value.Trim().ToLowerInvariant();

            if (!Choices(setting).Contains(word)) return false;

            switch (setting)
            {
                case Setting.TextSize:
                    if (!Enum.TryParse(word, true, out Lettering lettering)) return false;
                    how.Lettering = lettering;
                    return true;

                case Setting.Speech:
                    if (!Enum.TryParse(word, true, out Pace pace)) return false;
                    how.Speech = pace;
                    return true;

                case Setting.Contrast:
                    how.HighContrast = word == Adjustments.On;
                    return true;

                case Setting.Captions:
                    how.Captions = word == Adjustments.On;
                    return true;

                case Setting.ReadAloud:
                    how.ReadAloud = word == Adjustments.On;
                    return true;

                case Setting.Arms:
                    if (!Access.Arms.TryWord(word, out Arm arm)) return false;
                    how.Skin = arm;
                    return true;

                case Setting.ArmsShown:
                    if (!Access.Arms.TryWord(word, out Shown shown)) return false;
                    how.Showing = shown;
                    return true;

                default:
                    return false;
            }
        }

        public static bool TryWord(string word, out Setting setting)
        {
            setting = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Setting one in Enum.GetValues<Setting>())
            {
                if (Word(one) != trimmed) continue;

                setting = one;
                return true;
            }

            return false;
        }
    }
}

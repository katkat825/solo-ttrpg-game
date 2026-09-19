using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Core.Localization;

namespace Game.Book
{
    // THE BOOKCASE IS THE WORKSHOP DOOR, AND THESE ARE THE DOORS (BK6).
    //
    // A set of plain, blank objects standing on the bookcase beside your own things: a blank
    // campaign book, a blank class folio, a blank ability card, an unpainted mini, a plain building,
    // an unnumbered white die. Picking one up is entering the Workshop for that content type. That
    // is the in-game face of the downloadable-templates idea, mapped onto the content types the
    // engine actually has - and it is a diegetic answer to a browse screen, because a blank book on a
    // shelf is a thing rather than a storefront.
    //
    // WHAT IS HERE AND WHAT IS NOT. The blank objects, what each one IS, and which local skeleton it
    // copies from are all here and checkable. Publishing, subscribing and the Workshop UI are
    // Steam's: they need an app id, the SDK and two machines, and writing them against a mock would
    // mean lying to their own tests. So this is the door and not the room behind it - and the door is
    // the half that has to exist first either way.
    public enum Starter
    {
        // a blank campaign book: chapters, places, maps, monsters, people, quests, roads
        Campaign,

        // a blank class folio: a playable hero, its starting dice and its companion
        Classes,

        // a blank ability card. A folder of its own inside a class pack, because two classes in a
        // pack can share a riposte and filing it under either would make its key depend on load order
        Ability,

        // an unpainted mini
        Minis,

        // a plain building: a model that stands on the board rather than on a base
        Building,

        // an unnumbered white die - a dice skin
        Dice,
    }

    public static class Starters
    {
        public static string Word(this Starter starter) => starter.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words =>
            Enum.GetValues<Starter>().Select(Word).ToArray();

        public const string Subject = "blank";

        // ui.*, because the blank objects are the engine's own furniture. What a player MAKES with
        // one is theirs and names itself under its own pack id
        public static string NameKey(this Starter starter) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(starter), "name");

        public static IEnumerable<string> Keys()
        {
            foreach (Starter starter in Enum.GetValues<Starter>()) yield return NameKey(starter);
        }

        // what kind of folder a player ends up with. Ability and Building are not kinds of their own
        // - an ability lives in a class pack and a building in a mini pack - because a pack is a
        // folder and a campaign is one kind of one, rather than there being a folder per thing
        public static PackKind Kind(this Starter starter) => starter switch
        {
            Starter.Campaign => PackKind.Campaign,
            Starter.Classes => PackKind.Classes,
            Starter.Ability => PackKind.Classes,
            Starter.Minis => PackKind.Minis,
            Starter.Building => PackKind.Minis,
            _ => PackKind.Mixed,
        };

        // THE LOCAL SKELETON IT COPIES. templates/ holds a copy-ready, commented folder per pack
        // kind, and a blank object is that folder with a lid on it. Empty for a content type with
        // no skeleton yet, which is a gap named rather than papered over - see Waiting.
        public static string Skeleton(this Starter starter) => starter switch
        {
            Starter.Campaign => "my_campaign",
            Starter.Classes => "my_classes",
            Starter.Ability => "my_classes",
            Starter.Minis => "my_minis",
            Starter.Building => "my_minis",
            _ => "",
        };

        // the folder inside the skeleton a player of this kind actually fills in first
        public static string Fills(this Starter starter) => starter switch
        {
            Starter.Campaign => "places",
            Starter.Classes => "classes",
            Starter.Ability => "kits",
            Starter.Minis => "minis",
            Starter.Building => "models",
            _ => "",
        };

        // A BLANK OBJECT WITH NOTHING TO COPY, and the one there is: a dice skin is a .tres in the
        // engine's own Tray/skins/ and is not a pack at all today. Making it one is a content type
        // the engine does not have yet, which is engine work and not a string - so the object stands
        // on the bookcase and says so, rather than opening a Workshop onto nothing.
        public static bool Waiting(this Starter starter) => Skeleton(starter).Length == 0;

        public static IEnumerable<Starter> Ready =>
            Enum.GetValues<Starter>().Where(s => !s.Waiting());

        public static bool TryWord(string word, out Starter starter)
        {
            starter = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Starter one in Enum.GetValues<Starter>())
            {
                if (Word(one) != trimmed) continue;

                starter = one;
                return true;
            }

            return false;
        }
    }
}

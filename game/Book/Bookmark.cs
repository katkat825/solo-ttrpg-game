using System;
using Content.Saves;

namespace Game.Book
{
    // ONE CHARACTER, AS THE PLACE YOUR CAMPAIGN BOOK FALLS OPEN AT.
    //
    // Up to five per campaign, and a sixth ribbon that is blank (BK5). A character belongs to a
    // campaign playthrough; the collection - dice, minis, trophies - belongs to the room and is
    // shared across all of them.
    //
    // It is a view over the saves that already exist, not a record of its own. The newest save
    // bearing a name IS that character's place in the book, so there is nothing here that could
    // disagree with the folder - the same identity the shelf has had since R4.
    public sealed class Bookmark
    {
        // the blank ribbon. Named rather than null, so a bookcase can stand it up beside the
        // written ones and touching it is the same motion as touching any other
        public static Bookmark Blank() => new Bookmark("", null);

        public Bookmark(string who, Box latest)
        {
            Who = who ?? "";
            Latest = latest;
        }

        public string Who { get; }

        public Box Latest { get; }

        // a ribbon with nothing written on it: New Character, which is a blank sheet and not a
        // creation wizard (R0)
        public bool IsBlank => Latest == null;

        public DateTime Written => Latest?.Written ?? DateTime.MinValue;

        public string ClassId => Latest?.Save?.Sheet?.ClassId ?? "";

        public string Place => Latest?.Save?.Place ?? "";

        public bool Finished => Latest != null && Latest.Finished;

        public bool MidFight => Latest?.Save?.MidFight == true;

        // an unnamed character is a real character - the name line is a blank like any other and
        // nothing makes you fill it in. Only the absence of a save makes a ribbon blank.
        public bool Unnamed => !IsBlank && Who.Length == 0;

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            IsBlank
                ? "a blank ribbon - New Character"
                : $"{(Who.Length > 0 ? Who : "(unnamed)")}" +
                  (ClassId.Length > 0 ? $", {ClassId}" : "") +
                  (Place.Length > 0 ? $", at {Place}" : "") +
                  (Finished ? ", finished" : MidFight ? ", mid-fight" : "");
    }
}

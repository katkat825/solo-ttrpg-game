using System;

namespace Game.Book
{
    // A BOOK IN YOUR HANDS, AND WHICH PAGE IT IS OPEN AT.
    //
    // Pure, because everything worth getting right here is: a book opens at its contents, turning
    // to a page is only possible in the book that has that page, and going back is going back one
    // step rather than closing.
    //
    // THE RULES BOOK LAYS OVER THE CAMPAIGN BOOK AND DOES NOT LOSE YOUR PLACE. Pressing "?" in the
    // middle of reading the story so far and closing it again puts you back in the story so far,
    // because that is what happens when somebody hands you a second book - you do not drop the
    // first one. That is the whole reason this holds two and not one, and it is the difference
    // between a peer and a help overlay.
    public sealed class Opening
    {
        Held _under;

        Held _held;

        readonly struct Held
        {
            public Held(Tome tome, Page? page, Reference? chapter)
            {
                Tome = tome;
                Page = page;
                Chapter = chapter;
            }

            public Tome Tome { get; }

            public Page? Page { get; }

            public Reference? Chapter { get; }
        }

        bool _open;

        bool _beneath;

        public bool IsOpen => _open;

        public Tome? Which => _open ? _held.Tome : (Tome?)null;

        // null means the contents page, which is where a book opens and where Back goes
        public Page? At => _open && _held.Tome == Tome.Campaign ? _held.Page : null;

        public Reference? Chapter => _open && _held.Tome == Tome.Rules ? _held.Chapter : null;

        // at the front of whichever book is in your hands
        public bool AtTheContents => _open && _held.Page == null && _held.Chapter == null;

        // a campaign book still open under the rules book, waiting to be come back to
        public bool Beneath => _beneath;

        public bool Open(Tome tome)
        {
            if (_open && _held.Tome == tome) return false;

            // the rules book is handed to you on top of whatever you were reading; anything else
            // replaces what is in your hands, because you have two of them and one is holding the
            // pencil
            if (_open && tome == Tome.Rules)
            {
                _under = _held;
                _beneath = true;
            }
            else
            {
                _beneath = false;
            }

            _held = new Held(tome, null, null);
            _open = true;

            return true;
        }

        public bool TurnTo(Page page)
        {
            if (!_open || _held.Tome != Tome.Campaign) return false;

            if (_held.Page == page) return false;

            _held = new Held(Tome.Campaign, page, null);

            return true;
        }

        public bool TurnTo(Reference chapter)
        {
            if (!_open || _held.Tome != Tome.Rules) return false;

            if (_held.Chapter == chapter) return false;

            _held = new Held(Tome.Rules, null, chapter);

            return true;
        }

        // back one step: a page to the contents, the contents to whatever was underneath, and the
        // last book shut. Never further than one step, so there is nothing to get lost in
        public bool Back()
        {
            if (!_open) return false;

            if (!AtTheContents)
            {
                _held = new Held(_held.Tome, null, null);
                return true;
            }

            if (_beneath)
            {
                _held = _under;
                _beneath = false;
                return true;
            }

            return Close();
        }

        public bool Close()
        {
            if (!_open) return false;

            _open = false;
            _beneath = false;
            _held = default;
            _under = default;

            return true;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString()
        {
            if (!_open) return "shut";

            string where = _held.Page is { } page ? page.Word()
                         : _held.Chapter is { } chapter ? chapter.Word()
                         : "the contents";

            return $"{_held.Tome.Word()} open at {where}" +
                   (_beneath ? $", over {_under.Tome.Word()}" : "");
        }
    }
}

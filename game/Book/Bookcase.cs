using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Localization;
using Game.Localization;

namespace Game.Book
{
    // THE BOOKCASE (BK2) - the main area, a second corner of the same room.
    //
    // Every campaign lives on it as a book. What you have collected - dice, trays, minis - sits on it
    // beside them. And a row of plain, blank objects stands at the bottom, one per content type, each
    // of them the Workshop door for its kind (BK6).
    //
    // IT IS A CORNER OF THE ROOM AND NOT A SCREEN. This is the thing a main menu would have been, and
    // the test of whether the deletion is real is that you WALK to it: the camera leans over here
    // instead of over the table, the objects are objects, and nothing is pushed over the top of
    // anything. Sitting down is picking a book up and opening it.
    //
    // It stands what it is given and reports what it stood. What touching one MEANS is decided where
    // the objects are - in Room, in one place you can read - exactly as it is for furniture.
    [GlobalClass]
    public partial class Bookcase : Node3D
    {
        [Export] public float Width { get; set; } = 1.2f;

        [Export] public int Shelves { get; set; } = 3;

        [Export] public float ShelfGap { get; set; } = 0.30f;

        [Export] public float Deep { get; set; } = 0.20f;

        [Export] public Color Wood { get; set; } = new Color("#3d3124");

        // a book was lifted off it. Not 'Opened', which is the name of the open book
        // itself - Godot emits an event member per signal and the two would shadow
        [Signal] public delegate void LiftedEventHandler(string campaign, int tome);

        // a blank object was picked up: the Workshop for that content type. The Workshop itself is
        // Steam's, and needs an app id, the SDK and two machines - so this is the door, which is the
        // half that has to exist either way
        [Signal] public delegate void WorkshopEventHandler(int starter);

        readonly List<Book> _books = new List<Book>();

        readonly List<Blank> _blanks = new List<Blank>();

        readonly List<MeshInstance3D> _collected = new List<MeshInstance3D>();

        sealed class Blank
        {
            public Starter Is;
            public StaticBody3D Touch;
            public Node3D Body;
            public Vector3 Span;
            public string Called = "";
        }

        readonly ILocalizer _text = new GodotLocalizer();

        Node3D _carcass;

        public IReadOnlyList<Book> Books => _books;

        public int Blanks => _blanks.Count;

        public int Collected => _collected.Count;

        public string Called { get; private set; } = "";

        public override void _Ready()
        {
            Called = _text.Get(BookKeys.BookcaseName);

            Carcass();
        }

        // sides, a back and a shelf per row. Boxes, because nothing in this room is expensive
        void Carcass()
        {
            if (_carcass != null) return;

            _carcass = new Node3D { Name = "Carcass" };

            AddChild(_carcass);

            var finish = new StandardMaterial3D { AlbedoColor = Wood, Roughness = 0.92f };

            float tall = ShelfGap * Shelves + Board;

            for (int at = 0; at <= Shelves; at++)
                Slab(finish, $"Shelf{at}", new Vector3(Width, Board, Deep),
                     new Vector3(0f, ShelfGap * at, 0f));

            Slab(finish, "Left", new Vector3(Board, tall, Deep),
                 new Vector3(-Width * 0.5f, tall * 0.5f, 0f));

            Slab(finish, "Right", new Vector3(Board, tall, Deep),
                 new Vector3(Width * 0.5f, tall * 0.5f, 0f));

            Slab(finish, "Back", new Vector3(Width, tall, Board),
                 new Vector3(0f, tall * 0.5f, -Deep * 0.5f));
        }

        void Slab(StandardMaterial3D finish, string named, Vector3 size, Vector3 at) =>
            _carcass.AddChild(new MeshInstance3D
            {
                Name = named,
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = finish,
                Position = at,
            });

        public const float Board = 0.022f;

        // WHICH ROW HOLDS WHAT, said once rather than worked out three times in three methods that
        // could each be a shelf out. Rows are numbered from the floor up, and row 0 is the slab at
        // the bottom. The books go at eye level, the blanks at the bottom where a box of spares
        // lives, and what you have collected goes on TOP of the case, where a person actually puts
        // the things they are proud of.
        public int TopRow => Math.Max(0, Shelves - 1);

        public int BlankRow => 0;

        // the height a thing of this height sits at when it stands on that row's slab
        public float StandingOn(int row, float tall) => ShelfGap * row + Board * 0.5f + tall * 0.5f;

        public float OnTopOf(float tall) => ShelfGap * Shelves + Board * 0.5f + tall * 0.5f;


        // ---- the books ----------------------------------------------------------------------

        // EVERY CAMPAIGN IS A BOOK ON IT, installed or not. A save whose campaign is gone still
        // stands there: dropping somebody's playthrough because they unsubscribed from a Workshop
        // campaign is how a save folder stops being trustworthy.
        public void Stand(Collection collection)
        {
            foreach (Book book in _books) book.QueueFree();

            _books.Clear();

            if (collection == null) return;

            float along = -Width * 0.5f + Board + 0.03f;

            // eye level first, working down. The bottom row is the blanks, so books stop above it
            int shelf = TopRow;

            foreach (Volume volume in collection.Volumes)
            {
                // A VOLUME PER CHARACTER, which is what five characters per campaign looks like on a
                // shelf (BK5): a row of the same colour, one book per playthrough, and a paler one
                // at the end of the row that has nothing written in it yet.
                foreach (Bookmark ribbon in volume.Ribbons)
                {
                    if (along > Width * 0.5f - Board - 0.03f)
                    {
                        shelf--;
                        along = -Width * 0.5f + Board + 0.03f;

                        if (shelf <= BlankRow) return;
                    }

                    var book = new Book
                    {
                        Name = Spine + volume.Campaign + "_" + (ribbon.IsBlank ? "new" : ribbon.Who),
                        Campaign = volume.Campaign,
                        Is = Tome.Campaign,
                        Finished = ribbon.Finished,
                        Unwritten = ribbon.IsBlank,
                    };

                    book.Position = new Vector3(along, StandingOn(shelf, book.Tall), 0f);

                    AddChild(book);

                    _books.Add(book);

                    along += book.Thickness + 0.007f;
                }

                // a gap between campaigns, so a row reads as one campaign's volumes
                along += 0.018f;
            }
        }

        // THE RULES BOOK IS A PEER and stands here too, not only on the table's shelf (BK3)
        public Book StandTheRules()
        {
            var rules = new Book
            {
                Name = Spine + "rules",
                Is = Tome.Rules,
            };

            rules.Position = new Vector3(Width * 0.5f - Board - 0.04f,
                                        StandingOn(TopRow, rules.Tall), 0f);

            AddChild(rules);

            _books.Add(rules);

            return rules;
        }

        public const string Spine = "Book_";


        // ---- the blank objects, which are the Workshop door ---------------------------------

        // ONE PER CONTENT TYPE THE ENGINE HAS. Derived from the enum, so a content type added later
        // stands here the day it exists rather than the day somebody remembers this file.
        public void StandTheBlanks()
        {
            foreach (Blank one in _blanks)
            {
                one.Touch.QueueFree();
                one.Body.QueueFree();
            }

            _blanks.Clear();

            float along = -Width * 0.5f + Board + 0.05f;

            foreach (Starter starter in Enum.GetValues<Starter>())
            {
                float tall = Tall(starter);

                var holder = new Node3D
                {
                    Name = "Blank_" + starter.Word(),
                    Position = new Vector3(along, StandingOn(BlankRow, tall), 0f),
                };

                AddChild(holder);

                holder.AddChild(Shape(starter));

                var touch = new StaticBody3D { Name = "Touch" };

                // a hand's width whatever the thing is: an unnumbered die is 26mm across, and a
                // hitbox you have to aim at is an accessibility failure rather than a nicety
                Vector3 span = Game.Access.Hitbox.Around(
                    new Vector3(0.08f, Math.Max(tall, 0.05f), 0.16f));

                touch.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = span } });

                holder.AddChild(touch);

                _blanks.Add(new Blank
                {
                    Is = starter,
                    Touch = touch,
                    Body = holder,
                    Span = span,
                    Called = _text.Get(starter.NameKey()),
                });

                along += 0.095f;
            }
        }

        // how tall each blank thing is, so it stands ON the shelf rather than half through it
        static float Tall(Starter starter) => starter switch
        {
            Starter.Campaign => 0.20f,
            Starter.Classes => 0.17f,
            Starter.Ability => 0.002f,
            Starter.Minis => 0.06f,
            Starter.Building => 0.07f,
            _ => 0.026f,
        };

        // what each blank thing LOOKS like, because "a blank campaign book" and "an unpainted mini"
        // are different objects and a row of identical grey cubes would say they were the same one
        static MeshInstance3D Shape(Starter starter)
        {
            var bare = new StandardMaterial3D
            {
                AlbedoColor = Book.Unpainted,
                Roughness = 1.0f,
            };

            Mesh mesh = starter switch
            {
                // a book, standing, with nothing on the spine
                Starter.Campaign => new BoxMesh { Size = new Vector3(0.03f, 0.2f, 0.15f) },

                // a folio: flatter and wider than a campaign, because it is a folder of cards
                Starter.Classes => new BoxMesh { Size = new Vector3(0.02f, 0.17f, 0.13f) },

                // one card, lying
                Starter.Ability => new BoxMesh { Size = new Vector3(0.06f, 0.002f, 0.09f) },

                // a figure on a base
                Starter.Minis => new CapsuleMesh { Radius = 0.014f, Height = 0.06f },

                // a building, which is a bigger thing that stands on the board
                Starter.Building => new BoxMesh { Size = new Vector3(0.07f, 0.07f, 0.07f) },

                // an unnumbered die
                _ => new BoxMesh { Size = new Vector3(0.026f, 0.026f, 0.026f) },
            };

            return new MeshInstance3D
            {
                Name = "Body",
                Mesh = mesh,
                MaterialOverride = bare,
            };
        }


        // ---- what you have collected --------------------------------------------------------

        // Dice, trays and minis sit on the bookcase (BK2). Named rather than counted, so the shelf
        // is the collection and there is no second tally to disagree with what you own.
        public void StandTheCollection(IEnumerable<string> trays, IEnumerable<string> minis)
        {
            foreach (MeshInstance3D one in _collected) one.QueueFree();

            _collected.Clear();

            float along = -Width * 0.5f + Board + 0.04f;

            foreach (string tray in (trays ?? Enumerable.Empty<string>()).OrderBy(
                         t => t, StringComparer.Ordinal))
            {
                _collected.Add(Piece("Tray_" + tray, new BoxMesh
                {
                    Size = new Vector3(0.08f, 0.012f, 0.06f),
                }, new Color("#4a3a2a"), new Vector3(along, OnTopOf(0.012f), -0.04f)));

                along += 0.095f;
            }

            along = -Width * 0.5f + Board + 0.04f;

            foreach (string mini in (minis ?? Enumerable.Empty<string>()).OrderBy(
                         m => m, StringComparer.Ordinal))
            {
                _collected.Add(Piece("Mini_" + mini, new CapsuleMesh
                {
                    Radius = 0.010f,
                    Height = 0.042f,
                }, new Color("#7a6a55"), new Vector3(along, OnTopOf(0.042f), 0.05f)));

                along += 0.032f;
            }
        }

        MeshInstance3D Piece(string named, Mesh mesh, Color colour, Vector3 at)
        {
            var piece = new MeshInstance3D
            {
                Name = named,
                Mesh = mesh,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.9f },
                Position = at,
            };

            AddChild(piece);

            return piece;
        }


        // ---- touching things ----------------------------------------------------------------

        // EVERYTHING STANDING ON THE CASE, AS THINGS THAT CAN BE REACHED (AX1) - the books first and
        // then the blanks, which is the order the raycast asks in and therefore the order a hand walks.
        public IEnumerable<Game.Access.Reachable> Reachables()
        {
            foreach (Book book in _books) yield return book.Reach();

            foreach (Blank one in _blanks)
            {
                Starter starter = one.Is;

                yield return new Game.Access.Reachable(
                    one.Called, () => Pick(starter), one.Touch, one.Span);
            }
        }

        public string NameOf(Starter starter) =>
            _blanks.FirstOrDefault(b => b.Is == starter)?.Called ?? "";

        public void Pick(Book book)
        {
            if (book == null) return;

            EmitSignal(SignalName.Lifted, book.Campaign, (int)book.Is);
        }

        public void Pick(Starter starter) => EmitSignal(SignalName.Workshop, (int)starter);

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"bookcase: {_books.Count} book(s), {_blanks.Count} blank(s), " +
            $"{_collected.Count} thing(s) collected";
    }
}

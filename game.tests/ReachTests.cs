using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;
using Content.World;
using Core.Dice;
using Core.Space;
using Game.Explore;

namespace Game.Tests
{
    // WHAT A CLICK ON A SQUARE MEANS WHILE YOU ARE WALKING A PLACE.
    //
    // The board has answered this for a fight since C0 - beside a foe is a swing, anywhere else is
    // a move - and a walk has to answer it on the same board with the same click. There are four
    // answers and the whole of this file is that there are only four, and that each is the right
    // one: a square is walked to, somebody is walked UP TO, a way out is taken, and a way out that
    // this campaign has not opened says so rather than silently being a square.
    //
    // It runs on a real campaign read off disk through the same Package.Read the game uses, for
    // the reason every check in this project does: the thing measured has to be the thing played.
    public sealed class ReachTests : IDisposable
    {
        readonly string _root;

        public ReachTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "reach-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        const string Campaign = "quay";

        string Folder => Path.Combine(_root, Campaign);

        void Write(string relative, string text)
        {
            string path = Path.Combine(Folder, relative.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        // 1 is somebody standing there, 2 is the way out, 3 is a way out that is locked, 4 is the
        // door that unlocks it, and the hero starts in the far corner so nothing is reached
        // without walking to it
        const string Square = @"
+-+-+-+-+-+
|. 1 . 2 .|
+ + + + + +
|. . . . .|
+ + + + + +
|. 3 . 4 .|
+ + + + + +
|@ . . . .|
+-+-+-+-+-+
";

        static readonly Cell Start = new Cell(0, 3);

        static readonly Cell Norrel = new Cell(1, 0);

        static readonly Cell Way = new Cell(3, 0);

        static readonly Cell Locked = new Cell(1, 2);

        Exploring World()
        {
            Write(ManifestReader.FileName, $@"{{
                ""id"": ""{Campaign}"",
                ""format"": {ContentFormat.Current},
                ""engine"": ""{Core.EngineVersion.Current}"",
                ""chapters"": [ {{ ""id"": ""one"", ""places"": [ ""quay"", ""shed"" ] }} ]
            }}");

            Write("maps/quay.map", Square);
            Write("maps/shed.map", Square);

            Write("entities/norrel.json", @"{
                ""id"": ""norrel"",
                ""can"": { ""examine"": ""norrel_looks_up"" }
            }");

            // the thing that makes the locked way out open, so nothing in this campaign waits on
            // a fact nothing writes - the validator refuses that, and it is right to
            Write("entities/shed.json", @"{
                ""id"": ""shed"",
                ""can"": { ""open"": null }
            }");

            Write("places/quay.json", @"{
                ""id"": ""quay"",
                ""map"": ""quay"",
                ""standing"": [
                    { ""slot"": 1, ""entity"": ""norrel"" },
                    { ""slot"": 4, ""entity"": ""shed"" }
                ],
                ""exits"": [
                    { ""slot"": 2, ""to"": ""shed"" },
                    { ""slot"": 3, ""to"": ""shed"", ""when"": [ ""shed.open"" ] }
                ]
            }");

            Write("places/shed.json", @"{
                ""id"": ""shed"",
                ""map"": ""shed"",
                ""exits"": [ { ""slot"": 2, ""to"": ""quay"" } ]
            }");

            Package package = Package.Read(Folder);

            Assert.True(package.Clean,
                        string.Join("; ", package.Problems.Select(p => p.ToString())));

            return new Exploring(package, Facts.For(package.Id), new SeededRng(4242));
        }

        Exploring OnTheQuay()
        {
            Exploring world = World();

            Assert.False(world.Enter("quay").Refused);
            Assert.Equal(Start, world.Hero);

            return world;
        }

        static bool Beside(Cell a, Cell b) =>
            Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;


        // ---- nothing --------------------------------------------------------------------------

        [Fact]
        public void NowhereEnteredYetMeansNothing()
        {
            Reach reach = Reach.Of(World(), new Cell(2, 2));

            Assert.Equal(Means.Nothing, reach.Is);
            Assert.NotEqual("", reach.Why);
        }

        [Fact]
        public void ANullWorldIsNotACrash()
        {
            Assert.Equal(Means.Nothing, Reach.Of(null, new Cell(0, 0)).Is);
        }

        [Fact]
        public void OffTheMapIsNothing()
        {
            Assert.Equal(Means.Nothing, Reach.Of(OnTheQuay(), new Cell(40, 40)).Is);
        }

        [Fact]
        public void TheSquareTheHeroIsOnIsNothing()
        {
            Assert.Equal(Means.Nothing, Reach.Of(OnTheQuay(), Start).Is);
        }

        // THE FIGHT OWNS THE BOARD. Two things answering the same click is the bug this refuses
        [Fact]
        public void MidFightEveryClickIsNothing()
        {
            Exploring world = OnTheQuay();

            world.Begin(world.Standing(1));

            Assert.True(world.InAFight);

            foreach (Cell cell in new[] { new Cell(2, 2), Norrel, Way, Locked })
                Assert.Equal(Means.Nothing, Reach.Of(world, cell).Is);
        }


        // ---- walking --------------------------------------------------------------------------

        [Fact]
        public void AnEmptySquareIsWalkedTo()
        {
            Reach reach = Reach.Of(OnTheQuay(), new Cell(2, 2));

            Assert.Equal(Means.Walk, reach.Is);
            Assert.Equal(new Cell(2, 2), reach.Stand);
            Assert.Null(reach.It);
            Assert.Null(reach.Way);
        }

        [Fact]
        public void WorkingOutWhatAClickMeansDoesNotMoveTheHero()
        {
            Exploring world = OnTheQuay();

            Reach.Of(world, new Cell(2, 2));
            Reach.Of(world, Norrel);
            Reach.Of(world, Way);

            Assert.Equal(Start, world.Hero);
        }


        // ---- reaching somebody ------------------------------------------------------------------

        [Fact]
        public void SomebodyStandingThereIsWalkedUpTo()
        {
            Reach reach = Reach.Of(OnTheQuay(), Norrel);

            Assert.Equal(Means.Reach, reach.Is);
            Assert.Equal("norrel", reach.It.Id);
        }

        // you do not walk THROUGH somebody to talk to them
        [Fact]
        public void YouStopBesideThem_NeverOnTheirSquare()
        {
            Reach reach = Reach.Of(OnTheQuay(), Norrel);

            Assert.NotEqual(Norrel, reach.Stand);
            Assert.True(Beside(reach.Stand, Norrel),
                        $"{reach.Stand} is not next to {Norrel}");
        }

        [Fact]
        public void ItIsTheNearestSquareBesideThem()
        {
            Exploring world = OnTheQuay();

            Reach reach = Reach.Of(world, Norrel);

            int taken = world.Route(reach.Stand).Count;

            foreach (Cell side in new[]
                     {
                         new Cell(Norrel.X - 1, Norrel.Y), new Cell(Norrel.X + 1, Norrel.Y),
                         new Cell(Norrel.X, Norrel.Y + 1),
                     })
                Assert.True(world.Route(side).Count >= taken,
                            $"{side} was a shorter way to stand beside them");
        }

        [Fact]
        public void StandingRightNextToThemAlreadyIsNoWalkAtAll()
        {
            Exploring world = OnTheQuay();

            world.Walk(new Cell(1, 1));

            Reach reach = Reach.Of(world, Norrel);

            Assert.Equal(Means.Reach, reach.Is);
            Assert.Equal(new Cell(1, 1), reach.Stand);
        }

        // somebody the facts have taken off the table is a square again
        [Fact]
        public void OnceTheyAreGoneItIsJustASquare()
        {
            Exploring world = OnTheQuay();

            world.Learn("norrel.dead");

            Assert.Equal(Means.Walk, Reach.Of(world, Norrel).Is);
        }


        // ---- the ways out -------------------------------------------------------------------

        [Fact]
        public void AWayOutIsTakenRatherThanStoodOn()
        {
            Reach reach = Reach.Of(OnTheQuay(), Way);

            Assert.Equal(Means.Leave, reach.Is);
            Assert.Equal("shed", reach.Way.To);
            Assert.Equal(Way, reach.Stand);
        }

        // A LOCKED DOOR IS AN EXIT WHOSE REQUIREMENT IS NOT MET, not a missing one - so it is worth
        // saying, and saying it is what lets the DM narrate rather than the square going quiet
        [Fact]
        public void AWayOutThisCampaignHasNotOpenedSaysSo()
        {
            Reach reach = Reach.Of(OnTheQuay(), Locked);

            Assert.Equal(Means.Shut, reach.Is);
            Assert.Equal("shed", reach.Way.To);
        }

        [Fact]
        public void AndOpensWhenTheFactIsTrue()
        {
            Exploring world = OnTheQuay();

            world.Learn("shed.open");

            Assert.Equal(Means.Leave, Reach.Of(world, Locked).Is);
        }

        [Fact]
        public void EveryClickOnTheMapMeansExactlyOneThing()
        {
            Exploring world = OnTheQuay();

            foreach (Cell cell in world.Map.Cells)
            {
                Reach reach = Reach.Of(world, cell);

                Assert.True(Enum.IsDefined(reach.Is));

                // whatever it means, the square the hero ends up on is a square on this map
                Assert.True(world.Map.Contains(reach.Stand), $"{cell} put the hero off the map");
            }
        }
    }
}

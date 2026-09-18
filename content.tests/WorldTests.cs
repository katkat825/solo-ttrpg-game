using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Entities;
using Content.Places;
using Content.Quests;
using Content.Saves;
using Content.Schema;
using Content.World;
using Core.Dice;
using Core.Space;
using Xunit;

namespace Content.Tests
{
    // A WHOLE SMALL WORLD, WALKED THROUGH (PLACES_AND_PERSISTENCE.md section 10).
    //
    // The World layer cannot be chi-squared - you cannot statistically test a town - so what
    // correct means for it is this: every reference resolves, every quest is completable or
    // explicitly allowed to fail, and a place reconstructs identically from base map + facts after
    // a save and a load. All three are here, on a campaign written the way an author would write
    // one, through the same Package.Read the game runs.
    public sealed class WorldTests : IDisposable
    {
        readonly string _root;

        public WorldTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "world-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        const string Campaign = "hamlet";

        string Folder => Path.Combine(_root, Campaign);

        void Write(string relative, string text)
        {
            string path = Path.Combine(Folder, relative.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        // a square room with four spawns and the hero in the corner; the same reader the board uses
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

        Package World(string extra = "")
        {
            Write(ManifestReader.FileName, $@"{{
                ""id"": ""{Campaign}"",
                ""format"": {ContentFormat.Current},
                ""engine"": ""{Core.EngineVersion.Current}"",
                ""chapters"": [ {{ ""id"": ""one"", ""places"": [ ""square"", ""cellar"" ] }} ]
            }}");

            Write("maps/square.map", Square);
            Write("maps/cellar.map", Square);

            Write("monsters/rat.json", @"{
                ""id"": ""rat"", ""tier"": ""rabble"", ""vigor"": 1, ""defense"": 8,
                ""attributes"": { ""grace"": ""d6"" },
                ""gear"": { ""id"": ""teeth"", ""die"": ""d4"" }
            }");

            Write("monsters/bob_at_bay.json", @"{
                ""id"": ""bob_at_bay"", ""tier"": ""rival"", ""vigor"": 6, ""defense"": 9,
                ""attributes"": { ""might"": ""d8"" },
                ""gear"": { ""id"": ""stool"", ""die"": ""d6"" }
            }");

            Write("items/rock.json", @"{ ""id"": ""rock"", ""die"": ""d4"" }");

            Write("entities/bob.json", @"{
                ""id"": ""bob"",
                ""monster"": ""bob_at_bay"",
                ""can"": { ""examine"": ""bob_looks_up"", ""attack"": null }
            }");

            Write("entities/chest.json", @"{
                ""id"": ""chest"",
                ""can"": { ""search"": null },
                ""loot"": [ { ""item"": ""rock"", ""weight"": 1 } ]
            }");

            Write("entities/bobs_grave.json", @"{
                ""id"": ""bobs_grave"",
                ""can"": { ""examine"": ""dug_in_a_hurry"" }
            }");

            Write("places/square.json", @"{
                ""id"": ""square"",
                ""map"": ""square"",
                ""standing"": [
                    { ""slot"": 1, ""entity"": ""bob"" },
                    { ""slot"": 2, ""entity"": ""chest"" },
                    { ""slot"": 3, ""entity"": ""bobs_grave"", ""when"": [ ""bob.dead"" ] }
                ],
                ""exits"": [ { ""slot"": 4, ""to"": ""cellar"", ""arriving"": 1 } ],
                ""cues"": [
                    { ""when"": ""entered"", ""cue"": ""the_square"" },
                    { ""when"": ""fact"", ""fact"": ""bob.dead"", ""cue"": ""quieter_now"" }
                ]
            }");

            Write("places/cellar.json", @"{
                ""id"": ""cellar"",
                ""map"": ""cellar"",
                ""standing"": [
                    { ""slot"": 1, ""monster"": ""rat"" },
                    { ""slot"": 2, ""monster"": ""rat"" }
                ],
                ""exits"": [ { ""slot"": 4, ""to"": ""square"", ""arriving"": 4 } ],
                ""triggers"": [ { ""when"": ""cleared"", ""then"": ""ends"" } ]
            }");

            Write("quests/the_rock.json", @"{
                ""id"": ""the_rock"",
                ""offered"": { ""when"": [ ""square.visited"" ] },
                ""done"": { ""when"": [ ""chest.looted"" ], ""unless"": [ ""bob.dead"" ] },
                ""failed"": { ""when"": [ ""bob.dead"" ] }
            }");

            if (extra.Length > 0) Write(extra.Split('|')[0], extra.Split('|')[1]);

            return Package.Read(Folder);
        }

        static string Said(Package package) =>
            string.Join("; ", package.Problems.Select(p => p.ToString()));

        Exploring Walking(Package package, IRng rng = null) =>
            new Exploring(package, Facts.For(package.Id), rng ?? new SeededRng(4242));


        [Fact]
        public void AWholeWorldReadsWithNothingWrongWithIt()
        {
            Package package = World();

            Assert.True(package.Clean, Said(package));
            Assert.Equal(2, package.Places.Count);
            Assert.Equal(3, package.Entities.Count);
            Assert.Equal(1, package.Quests.Count);
        }

        [Fact]
        public void APlaceIsBaseMapPlusFactsApplied()
        {
            Exploring world = Walking(World());

            world.Enter("square");

            // the grave is not there, because Bob is not dead
            Assert.Equal(new[] { "bob", "chest" },
                         world.OnTheTable.OrderBy(p => p.Slot).Select(p => p.Id).ToArray());

            world.Facts.Set("bob.dead");
            world.Enter("square");

            Assert.Equal(new[] { "chest", "bobs_grave" },
                         world.OnTheTable.OrderBy(p => p.Slot).Select(p => p.Id).ToArray());
        }

        [Fact]
        public void TheEngineOffersOnlyTheVerbsTheAuthorPermitted()
        {
            Exploring world = Walking(World());

            world.Enter("square");

            Present bob = world.Standing(1);
            Present chest = world.Standing(2);

            Assert.Equal(new[] { Interaction.Attack, Interaction.Examine }, bob.Offers.OrderBy(v => v).ToArray());
            Assert.Equal(new[] { Interaction.Search }, chest.Offers.ToArray());

            // it was never offered, so it is refused - and the refusal is a developer sentence
            Doing no = world.Do(chest, Interaction.Talk);

            Assert.True(no.Refused);
        }

        [Fact]
        public void SearchingSomethingSpendsTheVerbAndRemembersThatItDid()
        {
            Exploring world = Walking(World());

            world.Enter("square");

            Doing did = world.Do(world.Standing(2), Interaction.Search);

            Assert.False(did.Refused);
            Assert.Equal("rock", Assert.Single(did.Took));
            Assert.Contains("chest.looted", did.Facts);

            Assert.Empty(world.Standing(2).Offers);
        }

        [Fact]
        public void LookingAtSomethingCanBeDoneAgainAndWritesNothingDown()
        {
            Exploring world = Walking(World());

            world.Enter("square");

            Doing once = world.Do(world.Standing(1), Interaction.Examine);
            Doing twice = world.Do(world.Standing(1), Interaction.Examine);

            Assert.Equal("dialogue.dm.narration.hamlet.bob_looks_up", once.Line);
            Assert.False(twice.Refused);
            Assert.Empty(once.Facts);
        }

        [Fact]
        public void AFightIsABoundedEpisodeAndBobStaysDeadAfterIt()
        {
            Exploring world = Walking(World());

            world.Enter("square");

            Doing swung = world.Do(world.Standing(1), Interaction.Attack);

            Assert.NotNull(swung.Fight);
            Assert.True(world.InAFight);
            Assert.Equal("hamlet.bob_at_bay", swung.Fight.Roster[0]);
            Assert.Equal("bob", swung.Fight.Entities[1]);

            // only the one you swung at; the chest is not a combatant
            Assert.Equal(1, swung.Fight.Foes);

            Ending ending = world.Fought(swung.Fight, won: true, down: new[] { 1 });

            Assert.False(world.InAFight);
            Assert.Contains("bob.dead", ending.Facts);
            Assert.True(world.Facts.Is("bob.dead"));

            // back into exploration on a persistent map, minus what the fight changed
            Assert.DoesNotContain(world.OnTheTable, p => p.Id == "bob");
            Assert.Contains(world.OnTheTable, p => p.Id == "bobs_grave");
        }

        [Fact]
        public void APreArmedRoomIsClearedOnceAndDoesNotReArmWhenYouWalkBackIn()
        {
            Exploring world = Walking(World());

            world.Enter("cellar");

            Assert.Equal(2, world.OnTheTable.Count);
            Assert.True(world.Where.IsAFight);

            Episode fight = world.Begin(null);

            Assert.Equal(2, fight.Foes);
            Assert.Empty(fight.Entities);

            Ending ending = world.Fought(fight, won: true);

            Assert.Contains("cellar.cleared", ending.Facts);
            Assert.Empty(world.OnTheTable);

            world.Enter("square");
            world.Enter("cellar");

            Assert.Empty(world.OnTheTable);
        }

        [Fact]
        public void AnAuthoredCueFiresWhenItsFactBecomesTrueAndOnlyOnce()
        {
            Exploring world = Walking(World());

            world.Enter("square");

            Assert.Equal(new[] { "the_square" }, world.OnTheTable.Count >= 0
                ? Cues(world.Enter("square")) : null);

            Doing swung = world.Do(world.Standing(1), Interaction.Attack);
            Ending ending = world.Fought(swung.Fight, won: true, down: new[] { 1 });

            Assert.Contains("quieter_now", Cues(ending));

            // still true, already said; standing in the room is not being told again
            Doing looked = world.Do(world.Standing(3), Interaction.Examine);

            Assert.DoesNotContain("quieter_now", Cues(looked));
        }

        static string[] Cues(Happened what) => what.Cues.Select(c => c.Id).ToArray();

        [Fact]
        public void TheQuestResolvesItselfWhenYouMurderTheManWhoSetIt()
        {
            Exploring world = Walking(World());

            world.Enter("square");

            Assert.Equal(QuestState.Offered, State(world));

            world.Accept("the_rock");
            Assert.Equal(QuestState.Active, State(world));

            world.Do(world.Standing(2), Interaction.Search);
            Assert.Equal(QuestState.Done, State(world));

            // and it un-does itself, because the turn-in depended on somebody who is now dead
            world.Facts.Set("bob.dead");
            Assert.Equal(QuestState.Failed, State(world));
        }

        static QuestState State(Exploring world) =>
            world.Quests.Of("the_rock").StateIn(world.Facts);

        // THE NEARBY-QUEST NUDGE. What the companion needs to know on the way in is derived, not
        // authored: a quest says which facts finish it, a place says what it can write, and the
        // overlap is the errand that ends here. Nothing in either file mentions the other.
        [Fact]
        public void AnAcceptedQuestThatCanBeFinishedHereIsWorthMentioningOnTheWayIn()
        {
            Exploring world = Walking(World());

            world.Enter("square");
            world.Accept("the_rock");

            Assert.Equal(new[] { "the_rock" }, world.TurningInHere().Select(q => q.Id).ToArray());

            // the arrival carries it, because walking in is the only moment worth saying it
            Assert.Contains("the_rock", world.Enter("square").Nearby);
        }

        [Fact]
        public void AQuestYouHaveNotAcceptedIsNotSomethingToBeStoppedAbout()
        {
            Exploring world = Walking(World());

            Assert.Empty(world.Enter("square").Nearby);
        }

        [Fact]
        public void NothingIsSaidInAPlaceThatCannotMoveTheQuestOn()
        {
            Exploring world = Walking(World());

            world.Enter("square");
            world.Accept("the_rock");

            // the cellar has two rats in it and nothing the turn-in is waiting on
            Assert.Empty(world.Enter("cellar").Nearby);
        }

        [Fact]
        public void AFinishedErrandIsNotMentionedAgainNextTimeYouWalkIn()
        {
            Exploring world = Walking(World());

            world.Enter("square");
            world.Accept("the_rock");
            world.Do(world.Standing(2), Interaction.Search);

            Assert.Equal(QuestState.Done, State(world));
            Assert.Empty(world.Enter("square").Nearby);
        }

        [Fact]
        public void AnExitLeadsSomewhereAndYouArriveWhereItSaid()
        {
            Exploring world = Walking(World());

            world.Enter("square");

            Exit way = Assert.Single(world.Ways);
            Going going = world.Take(way);

            Assert.Equal("cellar", going.To);
            Assert.False(going.Interrupted);

            Arrival there = world.Enter(going.To, going.Arriving);

            Assert.Equal("cellar", there.Place.Id);
            Assert.Equal(world.Map.SpawnAt(1), there.Hero);
        }

        [Fact]
        public void TheSameSquaresPathfindAndSeeAsTheyDoInAFight()
        {
            Exploring world = Walking(World());

            world.Enter("square");

            IReadOnlyList<Core.Space.Cell> route = world.Route(world.Map.SpawnAt(4).Value);

            Assert.NotNull(route);
            Assert.Equal(world.Hero, route[0]);
            Assert.True(world.CanSee(world.Map.SpawnAt(4).Value));

            // an occupied square is not somewhere to walk to, exactly as on the board
            Assert.Null(world.Route(world.Standing(1).At));
        }


        // THE SAVE ROUND TRIP (section 10, and P6's own discipline one level up): a save is the
        // fact set plus where you are, and a place comes back identically out of the two.
        [Fact]
        public void APlaceComesBackIdenticallyFromBaseMapPlusFactsAfterASaveAndALoad()
        {
            Package package = World();
            Exploring world = Walking(package);

            world.Enter("square");
            world.Do(world.Standing(2), Interaction.Search);

            Doing swung = world.Do(world.Standing(1), Interaction.Attack);
            world.Fought(swung.Fight, won: true, down: new[] { 1 });

            string[] before = world.OnTheTable.OrderBy(p => p.Slot)
                                   .Select(p => $"{p.Slot}:{p.Id}:{string.Join(",", p.Offers)}")
                                   .ToArray();

            var save = new SaveGame { Campaign = package.Id, Place = world.Where.Id };

            foreach (string fact in world.Facts.All) save.Facts.Add(fact);

            Read<SaveGame> back = SaveReader.Parse(SaveWriter.Write(save), "slot1.json");

            Assert.True(back.Ok, string.Join("; ", back.Problems.Select(p => p.ToString())));

            var loaded = Facts.For(package.Id);
            loaded.Absorb(back.Value.Facts);

            var again = new Exploring(package, loaded, new SeededRng(1));
            again.Enter(back.Value.Place);

            string[] after = again.OnTheTable.OrderBy(p => p.Slot)
                                  .Select(p => $"{p.Slot}:{p.Id}:{string.Join(",", p.Offers)}")
                                  .ToArray();

            Assert.Equal(before, after);
        }

        [Fact]
        public void ASaveKeepsTheFactsAndNothingAboutWhereTheMinisWere()
        {
            var save = new SaveGame { Campaign = "hamlet", Place = "square" };

            save.Facts.Add("bob.dead");
            save.Facts.Add("chest.looted");

            string written = SaveWriter.Write(save);

            Assert.Contains("\"place\": \"square\"", written);
            Assert.Contains("bob.dead", written);

            Read<SaveGame> back = SaveReader.Parse(written, "slot1.json");

            Assert.True(back.Ok);
            Assert.Equal(new[] { "bob.dead", "chest.looted" }, back.Value.Facts.ToArray());
        }

        [Fact]
        public void AFormatOneSaveStillNamesAPlace()
        {
            Read<SaveGame> back = SaveReader.Parse(@"{
                ""format"": 1, ""campaign"": ""ashfall"", ""encounter"": ""ash_yard""
            }", "old.json");

            Assert.True(back.Any);
            Assert.Equal("ash_yard", back.Value.Place);
            Assert.DoesNotContain(back.Problems, p => p.Where == "format");
        }


        // THE VALIDATOR IS THE WORLD LAYER'S ANSWER TO THE FAIRNESS SWEEP. Each of these is a
        // thing that compiles perfectly and is silently broken at the table.
        [Fact]
        public void AFactNothingEverMakesTrueIsRefused()
        {
            Package package = World("places/square.json|" + @"{
                ""id"": ""square"", ""map"": ""square"",
                ""standing"": [ { ""slot"": 1, ""entity"": ""bob"", ""when"": [ ""moon.risen"" ] } ]
            }");

            Assert.Contains(package.Problems,
                            p => p.What.Contains("ever makes 'moon.risen' true"));
        }

        [Fact]
        public void SomethingYouMayAttackNeedsAStatblockToBeAttackedAs()
        {
            Package package = World("entities/bob.json|" + @"{
                ""id"": ""bob"", ""can"": { ""attack"": null }
            }");

            Assert.Contains(package.Problems,
                            p => p.What.Contains("no statblock to be attacked as"));
        }

        [Fact]
        public void AVerbTheEngineDoesNotKnowIsRefusedAtLoadRatherThanAtTheTable()
        {
            Package package = World("entities/bob.json|" + @"{
                ""id"": ""bob"", ""can"": { ""seduce"": ""bob_blushes"" }
            }");

            Assert.Contains(package.Problems,
                            p => p.What.Contains("not something the engine knows how to do"));
        }

        [Fact]
        public void AnExitToNowhereIsNamed()
        {
            Package package = World("places/square.json|" + @"{
                ""id"": ""square"", ""map"": ""square"",
                ""standing"": [ { ""slot"": 1, ""entity"": ""bob"" } ],
                ""exits"": [ { ""slot"": 4, ""to"": ""the_moon"" } ]
            }");

            Assert.Contains(package.Problems,
                            p => p.Where == "exits[0].to" && p.What.Contains("the_moon"));
        }

        [Fact]
        public void AnAttackableTurnInIsACautionAndNotARefusal()
        {
            Package package = World();

            ContentProblem caution = Assert.Single(package.Cautions);

            Assert.Contains("attackable", caution.What);
            Assert.True(package.Clean, "a caution must never stop a campaign loading");
        }

        [Fact]
        public void TwoThingsClaimingTheSameKeyAreRefused()
        {
            Package package = World("quests/square.json|" + @"{
                ""id"": ""square"",
                ""done"": { ""when"": [ ""chest.looted"" ] }
            }");

            Assert.Contains(package.Problems,
                            p => p.What.Contains("already the id of a place"));
        }
    }
}

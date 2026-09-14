using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Saves;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Space;
using Game.Board;
using Game.Fight;
using Game.Tray;

using BoardNode = Game.Board.Board;
using FightNode = Game.Fight.Fight;

namespace Game.Saves
{
    // TAKING A SAVE OFF THE TABLE, AND PUTTING ONE BACK (CONTENT_PIPELINE.md P6).
    //
    // `Content.Saves` knows what a save IS and has no idea what a table is; this is the half that
    // knows both. It lives in `game/` for the same reason `CampaignLocale` does - it touches nodes
    // - and it is the only file that does, so the save format itself stays headless and testable.
    //
    // THE RESTORE IS NOT A REBUILD. A save does not describe a room; it describes what has
    // HAPPENED in one. So loading re-plays the ordinary setup - the board opens the campaign's
    // map, the fight musters the encounter's placements, the hero is equipped - and then this
    // puts the damage, the conditions, the positions and the turn back on top. That is why a save
    // is four kilobytes rather than a scene graph, and why a campaign that has been updated under
    // a save loads with the update rather than with a stale copy of itself.
    //
    // WHICH MEANS FOES ARE MATCHED, NOT CREATED. The encounter musters the same four it always
    // musters, and each is found in the save by the spawn slot it started on (`Piece.Slot`) -
    // unique where an id repeats and stable where a square moves. One that the save does not
    // mention had already fallen when the save was taken, and is taken off the board.
    public static class Savepoint
    {
        // ---- taking one ----

        public static SaveGame Of(FightNode fight, BoardNode board, DiceTray tray)
        {
            var save = new SaveGame
            {
                Campaign = board?.Campaign ?? "",
                Encounter = board?.Encounter ?? "",
                Chapter = ChapterOf(board),
                CampaignFormat = FormatOf(board),
            };

            if (fight == null) return save;

            Encounter running = fight.Encounter;

            if (running != null && running.Round > 0)
            {
                save.Round = running.Round;
                save.ActionsLeft = running.ActionsLeft;
            }

            // IN TURN ORDER, and seated as they go. The order IS the save's record of who goes
            // when - `Encounter.Resume` reads it straight back rather than re-deriving it from the
            // initiative numbers and a tie-break rule that is not in the file
            int seat = 0;

            foreach (Actor actor in Ordered(fight, running))
            {
                Piece piece = Standing(fight, actor);
                bool hero = ReferenceEquals(actor, fight.Hero);

                SavedActor saved = Capture(fight, board, actor, hero ? 0 : piece?.Slot ?? 0,
                                           running);

                saved.Seat = running != null && running.Round > 0 ? seat : -1;

                if (ReferenceEquals(actor, running?.Acting)) save.Turn = saved.Seat;

                if (hero) save.Hero = saved;
                else save.Foes.Add(saved);

                seat++;
            }

            foreach (TraySlot slot in tray?.Felt ?? System.Array.Empty<TraySlot>())
                save.Felt.Add(new SavedDie(slot.LabelKey, slot.Die, slot.Value));

            return save;
        }

        // THE FALLEN ARE LEFT OUT ENTIRELY rather than saved as down: a save is a record of what
        // is in the room, and a corpse is a thing the fight already took off the board. The load
        // puts them back the same way - anybody the encounter musters and the save does not
        // mention had already fallen.
        //
        // The hero is always in it, standing or not, because a save with no hero in it is not a
        // save of anything
        static IEnumerable<Actor> Ordered(FightNode fight, Encounter running)
        {
            if (running == null || running.Round == 0)
            {
                yield return fight.Hero;

                foreach (Actor foe in fight.Foes.Where(f => !f.IsDown)) yield return foe;

                yield break;
            }

            bool seen = false;

            foreach (Actor actor in running.Order)
            {
                if (ReferenceEquals(actor, fight.Hero)) { seen = true; yield return actor; continue; }

                if (!actor.IsDown) yield return actor;
            }

            if (!seen) yield return fight.Hero;
        }

        static Piece Standing(FightNode fight, Actor actor) =>
            fight.Pieces.FirstOrDefault(p => ReferenceEquals(p.Actor, actor));

        static SavedActor Capture(FightNode fight, BoardNode board, Actor actor, int slot,
                                  Encounter running)
        {
            var saved = new SavedActor
            {
                Id = actor.Id,
                Vigor = actor.Vigor,
                Nerve = actor.Nerve,
                Ordinal = actor.Ordinal,
                Slot = slot,
                Notches = actor.Notches,
                Strain = actor.Strain,
                Initiative = running?.InitiativeOf(actor) ?? 0,
                Wielded = PickedUp(actor.Wielded),
                Worn = PickedUp(actor.Worn),
            };

            foreach (Condition condition in actor.Conditions) saved.Conditions.Add(condition);

            foreach (string item in actor.Satchel) saved.Satchel.Add(item);

            Cell? at = board?.CellOf(fight.PieceFor(actor));

            if (at != null) { saved.X = at.Value.X; saved.Y = at.Value.Y; }

            return saved;
        }

        // ONLY GEAR THAT CAME OUT OF AN `items/` FOLDER. A monster's claw is written inline in
        // its own statblock and a hero with nothing in hand is holding `Gear.Nothing`, and neither
        // is a thing a save has any business carrying: both come back off the statblock when the
        // encounter musters again, and writing them down would be a save with a second, stale copy
        // of the campaign in it.
        //
        // What IS worth carrying is the axe he picked up off a ghoul - a catalogue item, which
        // the statblock knows nothing about and the save is the only record of
        static string PickedUp(Gear gear)
        {
            if (gear == null || gear.Id == Gear.Nothing.Id) return "";

            return Game.Campaigns.Library.Load(quiet: true).Items.Has(gear.Id) ? gear.Id : "";
        }

        static string ChapterOf(BoardNode board)
        {
            Content.Campaigns.Manifest manifest = ManifestOf(board);

            if (manifest == null || board.Encounter.Length == 0) return "";

            foreach (Content.Campaigns.Chapter chapter in manifest.Chapters)
                if (chapter.Encounters.Contains(board.Encounter)) return chapter.Id;

            return manifest.Start;
        }

        static int FormatOf(BoardNode board) => ManifestOf(board)?.Format ?? 0;

        static Content.Campaigns.Manifest ManifestOf(BoardNode board)
        {
            if (board == null || string.IsNullOrWhiteSpace(board.Campaign)) return null;

            return Game.Campaigns.Library.Load(quiet: true).Campaign(board.Campaign)?.Manifest;
        }

        // ---- and putting one back ----

        // Every problem is a thing the save asked for that the table could not give it, and none
        // of them stops the load. The caller shows them; the fight carries on (P6)
        public static IReadOnlyList<ContentProblem> Apply(SaveGame save, FightNode fight,
                                                          BoardNode board)
        {
            var problems = new List<ContentProblem>();

            if (save == null || fight == null) return problems;

            string file = "the save";

            // THE CAMPAIGN IS CHECKED FIRST AND IS THE ONE THING THAT CAN MAKE THE REST POINTLESS.
            // A save naming a campaign that is not installed any more - unsubscribed, or never
            // there - has a hero and no room to put him in, and the honest answer is to say which
            // campaign is missing and let the caller send the player back to the shelf
            if (save.Campaign.Length > 0)
            {
                Game.Campaigns.Loaded campaign =
                    Game.Campaigns.Library.Load(quiet: true).Campaign(save.Campaign);

                if (campaign == null)
                    problems.Add(new ContentProblem(
                        file, "campaign",
                        $"'{save.Campaign}' is not installed - this save has nowhere to be played, " +
                        "and the campaign it needs has to be put back first"));
                else if (campaign.Failed)
                    problems.Add(new ContentProblem(
                        file, "campaign", $"'{save.Campaign}' is installed and did not load"));
                else if (save.CampaignFormat > 0 &&
                         campaign.Manifest.Format != save.CampaignFormat)
                    problems.Add(new ContentProblem(
                        file, "campaign_format",
                        $"this save was made against {save.Campaign} content format " +
                        $"{save.CampaignFormat} and the installed one is " +
                        $"{campaign.Manifest.Format} - it has been updated underneath the save, " +
                        "and what still fits is loaded"));
            }

            var seated = new List<(int Seat, Actor Actor)>();

            if (save.Hero != null)
            {
                Restore(save.Hero, fight.Hero, fight, board, file, problems);
                seated.Add((save.Hero.Seat, fight.Hero));
            }

            RestoreFoes(save, fight, board, file, problems, seated);

            Resume(save, fight, seated, problems, file);

            return problems;
        }

        // THE TURN ORDER, PUT BACK RATHER THAN ROLLED AGAIN. `Encounter.Begin` throws for
        // initiative, which is exactly what a load must not do - re-rolling on load would let a
        // player reload until the order suited them. So the seats in the file become the order,
        // and `Encounter.Resume` takes it (see the comment there for what it forgives)
        static void Resume(SaveGame save, FightNode fight, List<(int Seat, Actor Actor)> seated,
                           List<ContentProblem> problems, string file)
        {
            if (!save.MidFight) return;

            Encounter running = fight.Encounter;

            if (running == null) return;

            var order = seated.OrderBy(s => s.Seat < 0 ? int.MaxValue : s.Seat)
                              .Select(s => s.Actor)
                              .ToList();

            int turn = order.FindIndex(a =>
                seated.Any(s => ReferenceEquals(s.Actor, a) && s.Seat == save.Turn));

            if (turn < 0)
            {
                problems.Add(new ContentProblem(
                    file, "turn",
                    $"this save says it is seat {save.Turn}'s turn and nobody is sitting there - " +
                    "the round starts from the top"));
                turn = 0;
            }

            var initiative = new Dictionary<Actor, int>(ReferenceEqualityComparer.Instance);

            if (save.Hero != null) initiative[fight.Hero] = save.Hero.Initiative;

            foreach ((int Seat, Actor Actor) one in seated)
                foreach (SavedActor saved in save.Foes)
                    if (saved.Seat == one.Seat && !ReferenceEquals(one.Actor, fight.Hero))
                        initiative[one.Actor] = saved.Initiative;

            running.Resume(order, save.Round, turn, save.ActionsLeft, initiative);
        }

        static void RestoreFoes(SaveGame save, FightNode fight, BoardNode board, string file,
                                List<ContentProblem> problems,
                                List<(int Seat, Actor Actor)> seated)
        {
            var taken = new HashSet<SavedActor>();

            foreach (Piece piece in fight.Pieces)
            {
                if (ReferenceEquals(piece.Actor, fight.Hero)) continue;

                SavedActor saved = Match(save, piece, taken);

                if (saved == null)
                {
                    // NOT IN THE SAVE MEANS ALREADY DEAD. The encounter musters everybody it
                    // always musters, so the ones the save does not mention are the ones that had
                    // already fallen - and the way to put that back is to do to them what the
                    // fight would have done
                    piece.Actor.RestoreVigor(0);
                    board?.Lift(piece.Mini);
                    continue;
                }

                taken.Add(saved);
                Restore(saved, piece.Actor, fight, board, file, problems);
                seated.Add((saved.Seat, piece.Actor));
            }

            foreach (SavedActor saved in save.Foes)
                if (!taken.Contains(saved))
                    problems.Add(new ContentProblem(
                        file, "foes",
                        $"'{saved.Id}' was on spawn {saved.Slot} in this save and this encounter " +
                        "does not place anybody there - it has been left out"));
        }

        // BY THE SPAWN SLOT FIRST, which is unique and never moves. By id and ordinal second, for
        // a fight whose foes were placed by a scene rather than by an encounter and therefore have
        // no slot at all - the shipped cellar, and every save taken in it
        static SavedActor Match(SaveGame save, Piece piece, HashSet<SavedActor> taken)
        {
            foreach (SavedActor saved in save.Foes)
            {
                if (taken.Contains(saved)) continue;
                if (piece.Slot > 0 && saved.Slot == piece.Slot) return saved;
            }

            if (piece.Slot > 0) return null;

            foreach (SavedActor saved in save.Foes)
            {
                if (taken.Contains(saved)) continue;
                if (saved.Slot > 0) continue;
                if (saved.Id == piece.Actor.Id && saved.Ordinal == piece.Actor.Ordinal) return saved;
            }

            return null;
        }

        // ONE ACTOR, PUT BACK. The order matters: gear, then the things that step dice down, then
        // the tracks. Conditions and strain each add a modifier through the F3 trait pipeline, so
        // re-applying them rebuilds exactly the dice the save was looking at - which is what "the
        // trait pipeline with provenance round-trips cleanly" means in practice
        static void Restore(SavedActor saved, Actor actor, FightNode fight, BoardNode board,
                            string file, List<ContentProblem> problems)
        {
            if (actor == null) return;

            if (saved.Id != actor.Id)
                problems.Add(new ContentProblem(
                    file, "id",
                    $"this save has '{saved.Id}' where the encounter musters '{actor.Id}' - what " +
                    "had happened to them is restored onto whoever is actually standing there"));

            Equip(saved, actor, file, problems);

            foreach (Condition condition in saved.Conditions) actor.ApplyCondition(condition);

            for (int i = 0; i < saved.Strain; i++) actor.AddStrain();

            // NOT CLAMPED HERE. `NotchGear` refuses at the d4 floor, which is the rule, so a save
            // asking for six notches on a d6 gets the two it can have and says so
            for (int i = 0; i < saved.Notches; i++)
                if (!actor.NotchGear())
                {
                    problems.Add(new ContentProblem(
                        file, "notches",
                        $"{actor.DebugName} is saved with {saved.Notches} notches and its weapon " +
                        $"reaches the floor after {i} - gear cannot be notched past a d4"));
                    break;
                }

            if (saved.Vigor >= 0) actor.RestoreVigor(saved.Vigor);

            actor.RestoreNerve(saved.Nerve);

            if (saved.Ordinal > 0) actor.Numbered(saved.Ordinal);

            Stand(saved, actor, fight, board, file, problems);
        }

        static void Equip(SavedActor saved, Actor actor, string file, List<ContentProblem> problems)
        {
            Game.Campaigns.Library library = Game.Campaigns.Library.Load(quiet: true);

            Wear(saved.Wielded, actor, library, wielded: true, file, problems);
            Wear(saved.Worn, actor, library, wielded: false, file, problems);

            foreach (string item in saved.Satchel)
            {
                // A NAME NOTHING CAN RESOLVE IS STILL CARRIED. `Actor.Satchel` is ids rather than
                // `Gear` precisely so an uninstalled campaign's trinket survives in the save
                // rather than being quietly dropped - reinstall the campaign and it is a thing
                // again (Actor.Satchel says this too)
                actor.Carry(item);

                if (!library.Items.Has(item))
                    problems.Add(new ContentProblem(
                        file, "satchel",
                        $"nothing installed ships an item called '{item}' - {actor.DebugName} is " +
                        "still carrying the name, and it will be a thing again if its campaign is"));
            }
        }

        static void Wear(string id, Actor actor, Game.Campaigns.Library library, bool wielded,
                         string file, List<ContentProblem> problems)
        {
            if (string.IsNullOrEmpty(id)) return;

            Gear gear = library.Items.Of(id);

            if (gear == null)
            {
                // the statblock's own is what is left, which is the same degradation an
                // uninstalled campaign produces everywhere else
                problems.Add(new ContentProblem(
                    file, wielded ? "wielded" : "worn",
                    $"nothing installed ships an item called '{id}' - {actor.DebugName} has what " +
                    "the roster gave them instead"));
                return;
            }

            if (wielded) actor.Wielding(gear);
            else actor.Wearing(gear);
        }

        static void Stand(SavedActor saved, Actor actor, FightNode fight, BoardNode board,
                          string file, List<ContentProblem> problems)
        {
            if (board == null) return;

            Mini mini = fight.PieceFor(actor);

            if (mini == null) return;

            if (!saved.OnTheBoard) { board.Lift(mini); return; }

            var cell = new Cell(saved.X.Value, saved.Y.Value);

            if (!board.Map.IsPassable(cell))
            {
                problems.Add(new ContentProblem(
                    file, "at",
                    $"{actor.DebugName} is saved on {cell} and nothing can stand there - left " +
                    "where the encounter put them"));
                return;
            }

            if (!board.Stand(mini, cell))
                problems.Add(new ContentProblem(
                    file, "at",
                    $"{actor.DebugName} is saved on {cell} and somebody else is already there - " +
                    "left where the encounter put them"));
        }
    }
}

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
    public static class Savepoint
    {

        public static SaveGame Of(FightNode fight, BoardNode board, DiceTray tray)
        {
            var save = new SaveGame
            {
                Campaign = board?.Campaign ?? "",
                Place = board?.Where ?? "",
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

            // in turn order: the sequence is the save's record of who goes when, read straight back rather than re-derived
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

        // the fallen are left out entirely; a corpse was already off the board, and the load re-fells anyone the save doesn't mention
        // the hero is always saved, standing or not
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

            // growth ids come off the board, not the actor: core has no concept of a growth step, and only the hero grows
            if (ReferenceEquals(actor, fight?.Hero) && board != null)
                foreach (string step in board.Grown) saved.Growth.Add(step);

            Cell? at = board?.CellOf(fight.PieceFor(actor));

            if (at != null) { saved.X = at.Value.X; saved.Y = at.Value.Y; }

            return saved;
        }

        // only catalogue gear (from an items/ folder): a statblock's own weapon comes back when the encounter musters, so saving it would be a stale copy of the campaign
        static string PickedUp(Gear gear)
        {
            if (gear == null || gear.Id == Gear.Nothing.Id) return "";

            return Game.Campaigns.Library.Load(quiet: true).Items.Has(gear.Id) ? gear.Id : "";
        }

        static string ChapterOf(BoardNode board)
        {
            Content.Campaigns.Manifest manifest = ManifestOf(board);

            if (manifest == null || board.Where.Length == 0) return "";

            foreach (Content.Campaigns.Chapter chapter in manifest.Chapters)
                if (chapter.Places.Contains(board.Where)) return chapter.Id;

            return manifest.Start;
        }

        static int FormatOf(BoardNode board) => ManifestOf(board)?.Format ?? 0;

        static Content.Campaigns.Manifest ManifestOf(BoardNode board)
        {
            if (board == null || string.IsNullOrWhiteSpace(board.Campaign)) return null;

            return Game.Campaigns.Library.Load(quiet: true).Campaign(board.Campaign)?.Manifest;
        }

        // each problem is something the save asked for that the table couldn't give; none stops the load
        public static IReadOnlyList<ContentProblem> Apply(SaveGame save, FightNode fight,
                                                          BoardNode board)
        {
            var problems = new List<ContentProblem>();

            if (save == null || fight == null) return problems;

            string file = "the save";

            // the campaign is checked first: a save whose campaign isn't installed has a hero and no room to put him in
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

        // turn order is put back, not rolled again: re-rolling initiative on load would let a player reload until the order suited them
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
                    // not in the save means already dead: fell before the save was taken, so re-felled here
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

        // match by spawn slot first (unique, never moves); by id and ordinal only for scene-placed foes with no slot
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

        // order matters: gear, then the things that step dice down, then the tracks, so re-applying rebuilds exactly the dice the save saw
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

            // not clamped here: NotchGear refuses at the d4 floor, so an over-notched save gets what it can and reports the rest
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
                // an unresolvable id is still carried: Satchel holds ids so an uninstalled campaign's trinket survives and returns on reinstall
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
                // fall back to the statblock's own gear, the same degradation an uninstalled campaign produces everywhere
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

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Entities;
using Content.Places;
using Content.Sheet;
using Content.World;
using Core.Localization;
using Core.Space;
using Game.Board;
using Game.Explore;
using Game.Localization;

namespace Game.Diagnostics
{
    // WALKS A PLACE ON THE REAL TABLE (Phase T).
    //
    // check-world.ps1 walks a world with no table at all, which is the proof the World layer is
    // Godot-free. This is the other half of that claim: the same world, on the shipped table, with
    // the mat swapping under the DM's hands, figures standing on it, cards laid out beside them and
    // a fight beginning and ending on the same map.
    //
    // WHAT IT DOES NOT DO IS FIGHT. check-fight.ps1 plays whole fights on the real felt and does it
    // properly; duplicating that here would be a second, worse copy of it. What this asserts is the
    // TRANSITION either way - that a swing becomes a real fight mustered from what was standing
    // there, and that the end of one becomes facts and figures back on the mat - and it forces the
    // end rather than playing to it, which is why the foes go down in one line below.
    //
    // It instances the real table.tscn, which instances the real board, tray, DM, companion and
    // sheet, for the reason every check in this project does: the thing measured has to be the
    // thing played.
    //
    // Everything printed is developer diagnostic, exempt from localization.
    public partial class TableCheck : HeadlessCheck
    {
        protected override string Subject => "table";

        [Export] public PackedScene TheTable { get; set; }

        [Export] public string CampaignId { get; set; } = "saltmarch";

        [Export] public string StartAt { get; set; } = "the_quay";

        // SEEDED SO THE LOCKBOX ACTUALLY DRAWS. Its table is one part glass to two parts nothing,
        // and a check that usually walks past the card path is a check that will one day stop
        // covering it without saying so. Drawing nothing is still a real outcome and still passes,
        // so -Seed is there to walk it the other way.
        [Export] public int Seed { get; set; } = 11;

        // the table stopped moving and the step it is on never came true
        [Export] public float StuckSeconds { get; set; } = 25f;

        readonly ILocalizer _text = new GodotLocalizer();

        Walkabout _walk;

        Game.Board.Board _board;

        Game.Dm.Dm _dm;

        Game.Room.Leaning _eye;

        Game.Room.Dressed _dressed;

        Game.Companion.Companion _friend;

        Node3D _table;

        Game.Room.Framing _frame;

        // where in the walk it is. Each one waits for the table to settle and then does its thing
        enum Step
        {
            Laid,
            Reaching,
            Answering,
            Searching,
            Leaning,
            Leaving,
            Swapped,
            Dressing,
            Swinging,
            Fighting,
            Fought,
            Over,
        }

        Step _step = Step.Laid;

        double _waited;

        bool _finished;

        // the place we set off from, so the swap can be told it changed something
        string _was = "";

        int _stoodOnTheQuay;

        public override void _Ready()
        {
            // the same three the world check takes, so a walk can be repeated on the table that a
            // person saw go wrong headlessly
            foreach (string arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--campaign=", StringComparison.Ordinal))
                    CampaignId = arg.Substring("--campaign=".Length).Trim();

                if (arg.StartsWith("--place=", StringComparison.Ordinal))
                    StartAt = arg.Substring("--place=".Length).Trim();

                if (arg.StartsWith("--seed=", StringComparison.Ordinal) &&
                    int.TryParse(arg.Substring("--seed=".Length).Trim(), out int seed))
                    Seed = seed;
            }

            if (TheTable == null)
            {
                Problem("no table scene was given to walk on");
                Conclude();
                return;
            }

            var table = TheTable.Instantiate<Node3D>();

            // set BEFORE the tree readies it, which is the only moment an export can still be told
            // something - and for the walk it is the only moment at all, because it catches the
            // scene's own fight in _EnterTree
            _walk = table.GetNodeOrNull<Walkabout>("Walkabout");

            if (_walk == null)
            {
                Problem("there is no Walkabout on this table, so the board still knows only a " +
                        "fight and a place cannot be walked at all");
                Conclude();
                return;
            }

            _walk.CampaignId = CampaignId;
            _walk.StartAt = StartAt;
            _walk.Seed = Seed;

            AddChild(table);

            _table = table;

            _frame = InThePicture.SeenThrough(table);

            _board = table.GetNodeOrNull<Game.Board.Board>("Board");
            _dm = table.GetNodeOrNull<Game.Dm.Dm>("Dm");
            _eye = table.GetNodeOrNull<Game.Room.Leaning>("Leaning");
            _dressed = table.GetNodeOrNull<Game.Room.Dressed>("Dressed");
            _friend = table.GetNodeOrNull<Game.Companion.Companion>("Companion");

            GD.Print("");
            GD.Print($"table   {_walk}");
        }

        public override void _Process(double delta)
        {
            if (_finished || _walk == null) return;

            _waited += delta;

            if (_waited > StuckSeconds)
            {
                Problem($"the table stopped moving at '{_step}' for {StuckSeconds}s - {_walk}");
                Conclude();
                return;
            }

            // nothing is asserted while the mat is in the air: a place half way across the table
            // is not a place anything is standing in
            if (_board?.Laid is { Swapping: true }) return;

            switch (_step)
            {
                case Step.Laid: TheMatIsDown(); break;
                case Step.Reaching: WalkUpToSomething(); break;
                case Step.Answering: TakeAnAnswer(); break;
                case Step.Searching: SearchSomething(); break;
                case Step.Leaning: LeanAndPickUp(); break;
                case Step.Leaving: TakeTheWayOut(); break;
                case Step.Swapped: TheMatSwapped(); break;
                case Step.Dressing: SwapAProp(); break;
                case Step.Swinging: SwingAtOne(); break;
                case Step.Fighting: EndIt(); break;
                case Step.Fought: TheFightIsOver(); break;
                default: Conclude(); break;
            }
        }

        void Next(Step step)
        {
            _step = step;
            _waited = 0.0;
        }


        // ---- the mat is down ----------------------------------------------------------------

        void TheMatIsDown()
        {
            Exploring world = _walk.World;

            if (world?.Where == null) return;

            GD.Print("");
            GD.Print($"place   {world.Where.Id} - {_board.Metrics}");

            if (_board.Map == null || _board.Map.Columns != world.Map.Columns ||
                _board.Map.Rows != world.Map.Rows)
                Problem($"the board is laid out {_board.Map?.Columns}x{_board.Map?.Rows} and " +
                        $"'{world.Where.Id}' is {world.Map.Columns}x{world.Map.Rows} - the mat on " +
                        "the table is not the place being walked");

            if (_board.CellOf(_board.Piece) != world.Hero)
                Problem($"the hero piece is on {_board.CellOf(_board.Piece)} and the world has " +
                        $"them on {world.Hero} - the mat and the walk disagree about where you are");

            // EVERYBODY PRESENT IS A FIGURE, not only the ones you could hit. That is the whole of
            // what the board learned here
            _stoodOnTheQuay = world.OnTheTable.Count;

            if (_walk.OnTheMat.Count != _stoodOnTheQuay)
                Problem($"{_stoodOnTheQuay} thing(s) are standing in '{world.Where.Id}' and " +
                        $"{_walk.OnTheMat.Count} figure(s) are on the mat");

            foreach (Present present in world.OnTheTable)
            {
                Mini figure = _walk.OnTheMat.Of(present.Slot);

                if (figure == null)
                {
                    Problem($"'{present.Id}' is standing on spawn {present.Slot} and there is no " +
                            "figure on the mat for it");
                    continue;
                }

                if (_board.CellOf(figure) != present.At)
                    Problem($"'{present.Id}' is on {present.At} and its figure is on " +
                            $"{_board.CellOf(figure)}");

                GD.Print($"        {present.Id,-20} spawn {present.Slot} on {present.At}" +
                         (present.IsAFoe ? " - a foe" :
                          $" - {string.Join(", ", present.Offers.Select(Interactions.Word))}"));
            }

            TheSheetOffersWhatTheSceneAllows(world.Where);

            Next(Step.Reaching);
        }

        // THE THREE YOU REACH FOR YOURSELF, and which of them this place has anybody to try on
        void TheSheetOffersWhatTheSceneAllows(Place here)
        {
            if (_walk.Paper == null)
            {
                Problem("there is no character sheet on this table, so the three checks you " +
                        "reach for yourself cannot be reached at all");
                return;
            }

            foreach (Check check in Checks.All)
            {
                bool allowed = here.Allows(check);

                if (_walk.Paper.Allowed(check) != allowed)
                    Problem($"'{here.Id}' {(allowed ? "allows" : "does not allow")} " +
                            $"{check.Word()} and the sheet " +
                            $"{(_walk.Paper.Allowed(check) ? "offers" : "does not offer")} it");

                if (!allowed) continue;

                string named = _text.Get(check.NameKey());

                if (named == check.NameKey())
                    Problem($"{check.Word()} is printed on the sheet as its own key - " +
                            "there are no words behind it");

                GD.Print($"sheet   you may try \"{named}\" here, against {here.Against(check)}");
            }
        }


        // ---- can you read it ------------------------------------------------------------------

        // the rule and the sweep are InThePicture's, shared with the fight check; this says when
        // to ask, which is every moment that puts words on the table
        void EveryWordIsInThePicture(string moment)
        {
            IReadOnlyList<string> lost = InThePicture.Unreadable(_table, _frame, out int read);

            foreach (string one in lost)
                Problem($"{one} on {moment} - a line the player cannot read is a line that was " +
                        "not said");

            GD.Print($"words   {read} line(s) on the table at {moment}");
        }


        // ---- walking up to something ---------------------------------------------------------

        void WalkUpToSomething()
        {
            Present it = _walk.World.OnTheTable.FirstOrDefault(p => !p.IsAFoe && p.Offers.Count > 0);

            if (it == null)
            {
                GD.Print("walk    nobody here offers anything, so there is nothing to answer");
                Next(Step.Leaving);
                return;
            }

            Reach reach = Reach.Of(_walk.World, it.At);

            if (reach.Is != Means.Reach)
            {
                Problem($"clicking '{it.Id}' on {it.At} meant '{reach.Is}' rather than walking up " +
                        $"to them - {reach.Why}");
                Conclude();
                return;
            }

            GD.Print("");
            GD.Print($"reach   {reach}");

            _walk.Touch(it.At);

            if (!_walk.Answers.Asking)
            {
                Problem($"walked up to '{it.Id}' and nothing was laid out to answer with - " +
                        "the verbs they offer have nowhere to be");
                Conclude();
                return;
            }

            Offer offer = _walk.Answers.Offered;

            // a handful of things you could DO to what is in front of you are CARDS
            if (offer.As != Laid.Cards)
                Problem($"'{it.Id}' offers {offer.Answers.Count} verb(s) and they went out on " +
                        $"{offer.As} - things you do to a thing on the table are cards");

            if (offer.Answers.Count != it.Offers.Count)
                Problem($"'{it.Id}' offers {it.Offers.Count} verb(s) and " +
                        $"{offer.Answers.Count} were laid out");

            for (int at = 0; at < _walk.Answers.Words.Count; at++)
            {
                string words = _walk.Answers.Words[at];

                if (words == offer.Answers[at].Key || words.Length == 0)
                    Problem($"the card for '{offer.Answers[at].Key}' has no words behind it - " +
                            "it reads as its own key");

                GD.Print($"card    [{at}] \"{words}\"");
            }

            EveryWordIsInThePicture("the verb cards");

            Next(Step.Answering);
        }

        void TakeAnAnswer()
        {
            Offer offer = _walk.Answers.Offered;

            int examine = IndexOf(offer, Interaction.Examine);

            if (examine < 0) examine = 0;

            string entity = offer.About;

            if (!_walk.Answers.Take(examine))
            {
                Problem($"the card for '{offer.At(examine)?.Key}' could not be taken");
                Conclude();
                return;
            }

            // AN OFFER IS MADE, ANSWERED AND GONE. A note or a row of cards that outlives the
            // moment is the panel this whole system exists to delete
            if (_walk.Answers.Asking && _walk.Answers.Offered.About != entity)
                GD.Print("answer  and what they still offer went back out, which is the moment " +
                         "continuing rather than a panel standing");

            string note = _dm?.Note ?? "";

            if (note.Length == 0)
                Problem($"'{entity}' was looked at and the DM pushed nothing across - the line " +
                        "the author wrote for it never reached the table");
            else
                GD.Print($"dm      \"{note}\"");

            EveryWordIsInThePicture("the DM's note");

            Next(Step.Searching);
        }


        // ---- what you gain comes across as a card ------------------------------------------------

        // ANYTHING YOU GAIN IS A CARD DEALT ACROSS, and the stack of them is the whole inventory.
        // A loot table may draw nothing, which is a real outcome and not a failure - so what is
        // asserted is that searching wrote the fact and that anything drawn arrived as a card.
        void SearchSomething()
        {
            _walk.Answers.Sweep();

            Present it = _walk.World.OnTheTable
                              .FirstOrDefault(p => p.Offering(Interaction.Search));

            if (it == null)
            {
                GD.Print("loot    there is nothing here to search");
                Next(Step.Leaning);
                return;
            }

            int held = _walk.Carrying?.Count ?? 0;

            GD.Print("");
            GD.Print($"loot    searching '{it.Id}'");

            _walk.Touch(it.At);

            int search = IndexOf(_walk.Answers.Offered, Interaction.Search);

            if (search < 0)
            {
                Problem($"'{it.Id}' offers search and no card was laid out for it");
                Conclude();
                return;
            }

            _walk.Answers.Take(search);

            string looted = Interaction.Search.Writes(it.Id);

            if (!_walk.World.Facts.Is(looted))
                Problem($"'{it.Id}' was searched and '{looted}' is not true - it can be searched " +
                        "again for ever");

            if (_walk.Carrying == null)
            {
                Problem("there is no stack beside the table, so anything gained has nowhere to go");
                Next(Step.Leaning);
                return;
            }

            if (_walk.Carrying.Count > held)
            {
                GD.Print($"loot    {_walk.Carrying}");

                if (!_walk.Carrying.Turn(_walk.Carrying.Count - 1))
                    Problem("a card was dealt and could not be turned over to read");
                else
                    GD.Print($"loot    turned over - {_walk.Carrying.Carrying.Reading}");
            }
            else
            {
                GD.Print("loot    the table drew nothing, which is a real outcome");
            }

            Next(Step.Leaning);
        }


        // ---- leaning in and picking up -------------------------------------------------------

        // ZOOM IS A THING YOU DO AS A PERSON AT THE TABLE. The one property worth a machine is that
        // looking away puts everything back - the eye to the table and what you were holding to
        // where it was lying.
        void LeanAndPickUp()
        {
            if (_eye == null)
            {
                Problem("there is no camera that can lean in, so the only way to look closely at " +
                        "anything would be a zoom control");
                Next(Step.Leaving);
                return;
            }

            GD.Print("");

            Node3D paper = _walk.Paper;

            Transform3D lay = paper?.GlobalTransform ?? default;

            if (paper != null)
            {
                if (!_eye.PickUp(paper))
                    Problem("the character sheet could not be picked up");
                else if (paper.GlobalTransform.Origin.IsEqualApprox(lay.Origin))
                    Problem("the sheet was picked up and did not move");
                else
                    GD.Print($"camera  the sheet came up from {lay.Origin} to " +
                             $"{paper.GlobalTransform.Origin}");
            }

            // the mat is a fixed thing: you lean over it rather than picking it up
            _eye.LeanOver(_board.ToGlobal(Vector3.Zero));

            if (!_eye.Moving) Problem("leaning over the mat moved nothing");

            _eye.Back();

            if (_eye.Held != null) Problem("looking away left something in your hands");

            if (paper != null && !paper.GlobalTransform.Origin.IsEqualApprox(lay.Origin))
                Problem("looking away did not put the sheet back where it was lying");
            else if (paper != null)
                GD.Print("camera  and looking away put it back on the table");

            Next(Step.Leaving);
        }

        static int IndexOf(Offer offer, Interaction verb)
        {
            for (int at = 0; at < offer.Answers.Count; at++)
                if (offer.Answers[at].Is == Answering.Verb && offer.Answers[at].Verb == verb)
                    return at;

            return -1;
        }


        // ---- the mat swaps ----------------------------------------------------------------------

        void TakeTheWayOut()
        {
            _walk.Answers.Sweep();

            Exit out_ = _walk.World.Ways.FirstOrDefault();

            if (out_ == null)
            {
                Problem($"there is no way out of '{_walk.World.Where.Id}', so the mat can never " +
                        "be swapped and a place change cannot be looked at");
                Conclude();
                return;
            }

            Cell? at = _walk.World.Map.SpawnAt(out_.Slot);

            if (at == null)
            {
                Problem($"the way out of '{_walk.World.Where.Id}' is on spawn {out_.Slot} and the " +
                        "map has no such slot");
                Conclude();
                return;
            }

            _was = _walk.World.Where.Id;

            GD.Print("");
            GD.Print($"leave   {_was} by spawn {out_.Slot} on {at.Value}");

            _walk.Touch(at.Value);

            Next(Step.Swapped);
        }

        void TheMatSwapped()
        {
            if (_walk.World.Where == null || _walk.World.Where.Id == _was) return;

            GD.Print("");
            GD.Print($"mat     {_was} came away and {_walk.World.Where.Id} went down");

            // THE HANDS DO IT. A place change that faded would be a screen wipe; the whole point
            // of a mat is that somebody lifts it
            IReadOnlyList<string> did = _dm?.Performed ?? Array.Empty<string>();

            int lifted = Last(did, Game.Board.MatSwap.Lifts.Word());
            int laid = Last(did, Game.Board.MatSwap.Lays.Word());

            if (lifted < 0 || laid < 0)
                Problem("the mat changed and the DM's hands did not " +
                        $"{Game.Board.MatSwap.Lifts.Word()} and {Game.Board.MatSwap.Lays.Word()} - " +
                        "a place change that nobody's hands did is a screen wipe");
            else if (laid < lifted)
                Problem("the new mat was laid down before the old one was lifted away");
            else
                GD.Print($"hands   {did[lifted]}, then {did[laid]}");

            // and the table is now the new place, with the new place's figures on it
            _step = Step.Laid;
            _waited = 0.0;

            TheMatIsDown();

            Next(Step.Dressing);
        }

        static int Last(IReadOnlyList<string> did, string word)
        {
            for (int at = did.Count - 1; at >= 0; at--)
                if (did[at].StartsWith(word, StringComparison.Ordinal)) return at;

            return -1;
        }


        // ---- the props, and the creature beside the table -----------------------------------

        // NONE OF THEM IS WELDED TO THE TABLE, and a swapped one is cosmetic. The tray is the one
        // prop that already wears skins for real, so it is the one this actually swaps.
        void SwapAProp()
        {
            GD.Print("");

            TheCompanionIsSized();

            if (_dressed == null)
            {
                Problem("nothing on this table can be swapped for another version of itself, so " +
                        "a campaign could never ship its own screen and nothing could be earned");
                Onward();
                return;
            }

            foreach (Game.Room.TableProp prop in Enum.GetValues<Game.Room.TableProp>())
                if (!_dressed.Wearing.IsPlain(prop))
                    Problem("the table is dressed before anything was swapped - " +
                            $"{Game.Room.TableProps.Word(prop)} " +
                            $"is wearing '{_dressed.Wearing.Wearing(prop)}'");

            var tray = GetTray();

            string felt = tray?.SkinName ?? "";

            string other = Game.Tray.TraySkin.All().FirstOrDefault(s => s != felt);

            if (other == null)
            {
                GD.Print("dressed only one felt ships, so there is nothing to swap the tray for");
                Onward();
                return;
            }

            if (!_dressed.Wear(Game.Room.TableProp.Tray, other))
                Problem($"the tray could not be swapped from '{felt}' to '{other}'");

            if (tray != null && tray.SkinName != other)
                Problem($"the tray was told to wear '{other}' and is wearing '{tray.SkinName}'");
            else
                GD.Print($"dressed the tray went from '{felt}' to '{other}' - " +
                         $"{_dressed.Wearing}");

            Onward();
        }

        Game.Tray.DiceTray GetTray()
        {
            foreach (Node child in GetChildren())
                if (child.GetNodeOrNull<Game.Tray.DiceTray>("DiceTray") is { } tray) return tray;

            return null;
        }

        // A WOLF, A RAVEN AND A RELIQUARY ARE NOT THE SAME SCALE. Size is on the card, so the thing
        // beside the table reads at its own size without a line of the scene changing
        void TheCompanionIsSized()
        {
            if (_friend?.Card == null)
            {
                GD.Print("companion nothing is sitting beside this table");
                return;
            }

            Node3D body = _friend.GetNodeOrNull<Node3D>("Body");

            if (body == null)
            {
                Problem("the companion has no body to be sized");
                return;
            }

            // THE CARD'S NUMBER IS RELATIVE, and it always was - what changed is that "normal" is
            // now a creature rather than a figurine. The placeholder was built at 60 mm against a
            // 75 mm hero mini, so the one living thing at this table was smaller than the painted
            // figures on the map; Presence is the one number that fixes that, and a card that
            // ships a smaller companion still gets a smaller one.
            float wanted = _friend.Card.Size * _friend.Presence;

            if (!Mathf.IsEqualApprox(body.Scale.X, wanted))
                Problem($"'{_friend.Card.Id}' is {_friend.Card.Size} on its card at a presence of " +
                        $"{_friend.Presence}, so it should read {wanted} beside the table and " +
                        $"reads {body.Scale.X}");
            else
                GD.Print($"companion {_friend.Card.Id} reads at {wanted:0.##} " +
                         $"({_friend.Card.Size:0.##} on its card) - on the " +
                         $"{Content.Companions.Perches.Word(_friend.Card.Perch)}");
        }

        void Onward()
        {
            if (_walk.World.Where.Standings.Any(st => st.IsAFoe)) Next(Step.Swinging);
            else Next(Step.Over);
        }


        // ---- a fight, begun and left on the same mat --------------------------------------------

        void SwingAtOne()
        {
            Present foe = _walk.World.OnTheTable.FirstOrDefault(p => p.IsAFoe);

            if (foe == null)
            {
                GD.Print("fight   nothing hostile is standing here after all");
                Next(Step.Over);
                return;
            }

            GD.Print("");
            GD.Print($"fight   swinging at '{foe.Id}' on {foe.At}");

            _walk.Touch(foe.At);

            if (_walk.Fighting == null)
            {
                Problem($"swung at '{foe.Id}' and no fight started on the board");
                Conclude();
                return;
            }

            if (!_walk.World.InAFight)
                Problem("a fight is on the board and the world does not think there is one");

            Next(Step.Fighting);
        }

        // FORCED, not played. check-fight.ps1 is the one that plays a fight; what is being
        // asserted here is only that the end of one comes back out onto the mat.
        void EndIt()
        {
            Game.Fight.Fight fight = _walk.Fighting;

            if (fight?.Encounter == null) return;

            if (fight.Foes.Count == 0)
            {
                Problem("the fight mustered nobody - the episode's roster never reached it");
                Conclude();
                return;
            }

            GD.Print($"fight   {fight.Foes.Count} foe(s) mustered: " +
                     string.Join(", ", fight.Foes.Select(f => f.DebugName)));

            foreach (Core.Characters.Actor foe in fight.Foes)
                if (!foe.IsDown) foe.Damage(foe.MaxVigor + 1);

            Next(Step.Fought);
        }

        void TheFightIsOver()
        {
            if (_walk.Fighting != null || _walk.World.InAFight) return;

            GD.Print("");
            GD.Print($"fight   over - {_walk.World}");

            string cleared = FactName.Cleared(_walk.World.Where.Id);

            if (!_walk.World.Facts.Is(cleared))
                Problem($"the fight in '{_walk.World.Where.Id}' was won and '{cleared}' is not " +
                        "true - nothing was written down, so walking back in re-arms it");

            if (_walk.World.OnTheTable.Any(p => p.IsAFoe))
                Problem("the fight is over and there are still foes standing in this place");

            if (_walk.OnTheMat.Count != _walk.World.OnTheTable.Count)
                Problem($"{_walk.World.OnTheTable.Count} thing(s) are left standing here and " +
                        $"{_walk.OnTheMat.Count} figure(s) are on the mat");

            if (_board.Claims == null)
                Problem("the fight ended and nothing claims a square - the board went back to " +
                        "walking its own piece rather than to being walked");

            GD.Print($"mat     {_walk.OnTheMat}");

            Next(Step.Over);
        }


        void Conclude()
        {
            if (_finished) return;

            _finished = true;

            GD.Print("");
            GD.Print($"walked  {string.Join(" -> ", _walk?.Been ?? Array.Empty<string>())}");
            GD.Print("");

            Finish();
        }
    }
}

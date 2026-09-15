using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Campaigns;
using Content.Dialogue;
using Core.Dice;
using Core.Localization;
using Game.Localization;

namespace Game.Diagnostics
{
    // PLAYS EVERY CONVERSATION EVERY CAMPAIGN SHIPS, HEADLESS (Phase W).
    //
    // A bark bank that passes a compiler can still sound like nobody, and no check will ever tell
    // you whether it does - that part of W's verify list is a reading test and stays one. What a
    // machine CAN hold is everything around the words:
    //
    //   - every conversation runs from its first line to its end, taking every branch it is
    //     offered, without the runtime complaining
    //   - every line that comes out has words behind it in this locale
    //   - the words came through the LOCALIZER and not out of the .yarn file, which is the one
    //     claim W0 makes and the only one that is easy to break by accident. Proved by running
    //     the whole thing twice, once in the pseudolocale: a line that does not mangle is a line
    //     being read out of the campaign folder
    //   - every beat of the shared spine is phrased by every voice on the shelf - W5's "no
    //     companion may withhold a beat the player needs", checked rather than promised
    //   - every rung of every hint ladder reaches a beat that exists and has words
    //
    // It is NOT a second reader. Package.Read is the validator and Conversation is the runtime the
    // game plays; this runs both of them.
    //
    // Everything printed is developer and author diagnostic, exempt from localization.
    public partial class DialogueCheck : HeadlessCheck
    {
        protected override string Subject => "dialogue";

        // a shelf with no dialogue on it is not a failure - most packs are minis or classes
        [Export] public bool ExpectSome { get; set; }

        // how many lines a single conversation may produce before it is called a loop
        [Export] public int MostLines { get; set; } = 400;

        // how many ways a fork is walked. Four covers every branching choice anybody should be
        // writing; a fifth option on one question is a question that wanted to be two
        [Export] public int Branches { get; set; } = 4;

        readonly ILocalizer _text = new GodotLocalizer();

        public override void _Ready()
        {
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg == "--expect-some") ExpectSome = true;

            Game.Campaigns.Library library = Game.Campaigns.Library.Load();

            string[] voices = library.Voices.ToArray();

            GD.Print("");
            GD.Print($"voices  {(voices.Length == 0 ? "none on this shelf" : string.Join(", ", voices))}");

            int conversations = 0;
            int lines = 0;

            foreach (Game.Campaigns.Loaded campaign in library.InPlay)
            {
                if (campaign.Dialogue.Program == null && campaign.Beats.Count == 0 &&
                    campaign.Hints.Count == 0)
                    continue;

                GD.Print("");
                GD.Print($"campaign {campaign.Id}");

                conversations += Walk(campaign, ref lines);

                Spine(campaign, voices);

                Ladders(campaign, voices);
            }

            GD.Print("");
            GD.Print($"played  {conversations} conversation(s), {lines} line(s)");

            if (conversations == 0 && ExpectSome)
                Problem("nothing on this shelf has a word of dialogue in it, and this build expects some");

            GD.Print("");
            Finish();
        }

        // every node, played through, in both locales
        int Walk(Game.Campaigns.Loaded campaign, ref int lines)
        {
            if (campaign.Dialogue.Program == null) return 0;

            int played = 0;

            foreach (string node in campaign.Dialogue.Nodes.OrderBy(n => n, System.StringComparer.Ordinal))
            {
                Topic? topic = campaign.Dialogue.TopicOf(node);

                // once per branch position, so a three-way fork is walked all three ways
                var heard = new List<string>();
                bool ranAway = false;

                for (int prefer = 0; prefer < Branches; prefer++)
                {
                    IReadOnlyList<string> path =
                        Game.Dialogue.Talk.Walk(campaign.Dialogue, node, prefer, MostLines);

                    if (path.Count >= MostLines) ranAway = true;

                    foreach (string key in path)
                        if (!heard.Contains(key)) heard.Add(key);
                }

                GD.Print($"  {node} ({campaign.Dialogue.SpeakerOf(node)}" +
                         (topic is { } about ? $", camp: {about.Word()}" : "") +
                         $") - {heard.Count} line(s) over {Branches} walk(s)");

                if (heard.Count == 0)
                {
                    Problem($"{campaign.Id}/{node} produced no lines at all - the conversation " +
                            "starts and immediately ends, which is not a conversation");
                    continue;
                }

                if (ranAway)
                    Problem($"{campaign.Id}/{node} was still going after {MostLines} lines - " +
                            "something in it jumps back into itself with nothing to stop it");

                foreach (string key in heard) Says(campaign.Id, node, key);

                lines += heard.Count;
                played++;
            }

            if (campaign.Dialogue.CampNodes.Any()) Nights(campaign);

            return played;
        }

        // the fire has to have something to say on an ordinary night, or most nights are silent
        void Nights(Game.Campaigns.Loaded campaign)
        {
            var fire = new Campfire(campaign.Dialogue, new SeededRng(4242));

            GD.Print($"  camp    {fire}");

            if (!fire.Has(Topic.Quiet))
                Problem($"{campaign.Id} has camp scenes and none of them is a '{Topic.Quiet.Word()}' " +
                        "one - a night falls back to quiet and to nothing else, because every " +
                        "other topic claims something about a day the companion would then be " +
                        "wrong about. Most nights are quiet ones");
        }

        // W5: one intent, written once, and EVERY voice has its own phrasing of it
        void Spine(Game.Campaigns.Loaded campaign, IReadOnlyList<string> voices)
        {
            if (campaign.Beats.Count == 0) return;

            GD.Print($"  spine   {campaign.Beats.Count} beats x {voices.Count} voice(s)");

            foreach (Beat beat in campaign.Beats.All)
            {
                // colour is texture; a voice with no opinion about the Goose Incident is fine
                if (!beat.MustBeDelivered) continue;

                if (campaign.Beats.Reaches(beat, voices, _text.Has))
                {
                    // deliverable, but say who will be hearing it from the DM rather than from
                    // the creature they brought - that is the flavour still worth writing
                    string[] quiet = campaign.Beats.Silent(beat, voices, _text.Has).ToArray();

                    if (quiet.Length > 0)
                        GD.Print($"          '{beat.Id}' falls to the DM for " +
                                 string.Join(", ", quiet));

                    continue;
                }

                Problem($"{campaign.Id}: the '{beat.Kind.Word()}' beat '{beat.Id}' cannot be " +
                        $"delivered to a player who brought {string.Join(" or ", campaign.Beats.Silent(beat, voices, _text.Has))}" +
                        (beat.Note.Length > 0 ? $" - the intent is \"{beat.Note}\"" : "") +
                        $". Write it for them, or write the DM's version ({beat.DmKey(campaign.Id)}) " +
                        "so nobody is left without it");
            }
        }

        void Ladders(Game.Campaigns.Loaded campaign, IReadOnlyList<string> voices)
        {
            if (campaign.Hints.Count == 0) return;

            GD.Print($"  hints   {campaign.Hints.Count} ladder(s), {HintLadder.Rungs} rungs each");

            foreach (Hint hint in campaign.Hints.All)
            {
                // the ladder is walked the way a player walks it, and the fourth ask has to escalate
                var ladder = new HintLadder();

                for (int ask = 1; ask <= HintLadder.Rungs; ask++)
                {
                    Ask rung = ladder.Next(hint.Id);

                    if (rung.Rung != ask)
                    {
                        Problem($"{campaign.Id}: asking about '{hint.Id}' {ask} times landed on " +
                                $"{rung} rather than rung {ask}");
                        break;
                    }

                    // the rungs are beats and Spine already held them to being deliverable; what
                    // is checked here is that the ladder reaches one at all
                    if (campaign.Beats.Of(hint.Beat(rung.Rung)) == null)
                        Problem($"{campaign.Id}: rung {ask} of '{hint.Id}' names the beat " +
                                $"'{hint.Beat(rung.Rung)}' and this campaign has no such beat");
                }

                if (!ladder.Next(hint.Id).Overasked)
                    Problem($"{campaign.Id}: asking about '{hint.Id}' a fourth time did not read " +
                            "as over-asked, so the ladder repeats itself instead of the companion " +
                            "noticing");
            }

            // the over-asked line is the companion's own, not the campaign's, so it is checked
            // against the voice rather than the folder
            foreach (string voice in voices)
                if (!_text.Has(DialogueKeys.Bark(voice, Bark.Overasked, 1)))
                    Problem($"{voice} has hint ladders pointed at it and no '{Bark.Overasked.Word()}' " +
                            "bark - asking a fourth time would be silence rather than the " +
                            "companion saying it has had enough");
        }

        // the line resolves, and it resolves THROUGH THE LOCALIZER - which is the claim W0 makes
        void Says(string campaign, string node, string key)
        {
            if (!_text.Has(key))
            {
                Problem($"{campaign}/{node} says {key} and nothing in this locale has words for it");
                return;
            }

            bool was = TranslationServer.PseudolocalizationEnabled;

            TranslationServer.PseudolocalizationEnabled = false;
            string plain = _text.Get(key);

            TranslationServer.PseudolocalizationEnabled = true;
            string pseudo = _text.Get(key);

            TranslationServer.PseudolocalizationEnabled = was;

            if (plain != pseudo) return;

            Problem($"{campaign}/{node}: {key} reads the same in the pseudolocale as in English, " +
                    "so it did not come through the translation system - a line shown from the " +
                    ".yarn source is English hard-coded in a data file");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Core.Localization;
using Game.Access;
using Game.Audio;
using Game.Book;
using Game.Localization;

// Game.Room is both a namespace and the type; alias so Room binds to the node
using RoomNode = Game.Room.Room;

namespace Game.Diagnostics
{
    // BOOTS THE REAL ROOM AND PLAYS IT WITH NO MOUSE, NO EYES AND NO EARS (Phase AX).
    //
    // Accessibility is a standing requirement, which means it needs what "no menus" has: something
    // that fails when a later milestone quietly breaks it. Most of AX is an eye-and-ear check a person
    // does - whether it actually reads well aloud, whether high contrast is contrast - but the part
    // underneath is mechanical, and this is that part:
    //
    //   - EVERY REACHABLE THING HAS A NAME, A BODY AND A BODY BIG ENOUGH TO HIT. A thing with no name
    //     is silent to a screen reader; a thing too small to hit is a thing a player cannot do, because
    //     there is no menu to fall back on
    //   - THE HAND CAN GET TO ALL OF THEM AND BACK ROUND. Walking the ring visits every live thing
    //     exactly once, which is the difference between keyboard navigation and a keyboard trap
    //   - NOTHING SAID OUT LOUD IS STILL A KEY. A missing string on screen reads as ui.door.name and
    //     is impossible to miss; in a voice it is a noise, and this is the only thing that would catch
    //     it
    //   - EVERY DIAL ON THE SETTINGS PAGE TURNS, MEANS SOMETHING, AND IS STILL TRUE NEXT TIME
    //   - EVERY ACT HAS A KEY, AND NO TWO ACTS SHARE ONE
    //   - EVERY COLOURED CUE HAS A TWIN THAT IS NOT A COLOUR
    //   - THE ROOM IS DARK AT MIDNIGHT IN DECEMBER AND IS NOT AT NOON IN JUNE
    //
    // It instances the real room.tscn, which instances the real table.tscn, for the reason every check
    // in this project does: the thing measured has to be the thing played.
    //
    // Everything printed is developer diagnostic, exempt from localization.
    public partial class AccessCheck : HeadlessCheck
    {
        protected override string Subject => "access";

        [Export] public PackedScene TheRoom { get; set; }

        // a settings file of its own, never the player's: this check turns every dial in the game
        [Export] public bool UseAScratchFolder { get; set; } = true;

        readonly ILocalizer _text = new GodotLocalizer();

        string _scratch = "";

        public override void _Ready()
        {
            RoomNode room = Stand();

            if (room == null) { Finish(); return; }

            Reaching reach = room.Reach;

            if (reach == null)
            {
                Problem("there is no Reaching in the shipped room, so nothing in it can be played " +
                        "by keyboard and nothing can be read out");
                Finish();
                return;
            }

            EverythingHasANameAndABody(room);

            TheHandGetsEverywhere(reach, room);

            NothingSaidIsAKey(reach, room);

            EveryActHasAKey(reach);

            TheDialsTurn(reach, room);

            TheLettersResize(reach, room);

            NoMeaningInColourAlone();

            TheSoundsHaveWords(reach);

            YourArms(reach);

            WhereAreWe(room);

            TheHourOfTheDay();

            Tidy();

            GD.Print("");
            Finish();
        }

        RoomNode Stand()
        {
            if (TheRoom == null)
            {
                Problem("no room scene was given to stand in");
                return null;
            }

            var room = TheRoom.Instantiate<RoomNode>();

            if (room == null)
            {
                Problem("the room scene did not instance as a Room");
                return null;
            }

            // told before the tree readies it, which is the only moment an export can still be heard:
            // after AddChild the room has read its shelf and the Reaching has read its settings
            if (UseAScratchFolder)
            {
                _scratch = Scratch();

                room.SavesFolder = Path.Combine(_scratch, "saves");

                Reaching reach = room.GetNodeOrNull<Reaching>("Reaching");

                if (reach != null) reach.SettingsFolder = _scratch;
            }

            AddChild(room);

            GD.Print("");
            GD.Print($"room    {room}");

            return room;
        }

        string Scratch()
        {
            string folder = Path.Combine(Path.GetTempPath(),
                                         "access_check_" + DateTime.Now.ToString("HHmmss"));

            Directory.CreateDirectory(folder);

            return folder;
        }

        void Tidy()
        {
            if (_scratch.Length == 0) return;

            try
            {
                Directory.Delete(_scratch, true);
            }
            catch (IOException)
            {
                // a scratch folder left behind is not a failure of the game
            }
        }


        // ---- every reachable thing -------------------------------------------------------------

        void EverythingHasANameAndABody(RoomNode room)
        {
            IReadOnlyList<Reachable> all = room.Reachables();

            GD.Print("");
            GD.Print($"reach   {all.Count} thing(s) in the room can be reached");

            if (all.Count == 0)
                Problem("nothing in the room can be reached at all");

            foreach (Reachable one in all)
            {
                if (one.Called.Length == 0)
                {
                    Problem($"something reachable has no name ({one}), so a screen reader has " +
                            "nothing to say about it and the room is silent where it stands");
                    continue;
                }

                if (Spoken.IsAKey(one.Called))
                    Problem($"'{one.Called}' is reachable and its name is still a key - it has no " +
                            "words in this locale, and in a voice a key is just a noise");

                if (one.Body == null)
                    Problem($"'{one.Called}' is reachable and has no body, so a click can never " +
                            "land on it");

                if (!Hitbox.Generous(one.Span))
                    Problem($"'{one.Called}' is {one.Span.X * 1000f:0}x{one.Span.Z * 1000f:0}mm, " +
                            $"{Hitbox.Short(one.Span) * 1000f:0}mm short of the " +
                            $"{Hitbox.Least * 1000f:0}mm a fingertip needs");
            }

            foreach (Reachable one in all.Take(8))
                GD.Print($"        {one}");

            if (all.Count > 8) GD.Print($"        ... and {all.Count - 8} more");
        }

        void TheHandGetsEverywhere(Reaching reach, RoomNode room)
        {
            Pointing hand = reach.Hand;

            GD.Print("");

            if (hand.Count != room.Reachables().Count)
                Problem($"the hand knows about {hand.Count} thing(s) and the room has " +
                        $"{room.Reachables().Count} - a keyboard and a mouse are reaching " +
                        "different rooms");

            if (hand.Live == 0)
            {
                Problem("nothing in the room can be reached by keyboard");
                return;
            }

            // round the whole ring: every live thing exactly once, and back where it started
            var visited = new List<string>();

            for (int step = 0; step < hand.Live; step++)
            {
                if (!hand.Next())
                {
                    Problem($"the hand stopped after {step} of {hand.Live} - a keyboard player is " +
                            "stuck wherever that is");
                    return;
                }

                visited.Add(hand.On.Called);
            }

            GD.Print($"hand    reached {visited.Count} thing(s) in a ring: " +
                     string.Join(", ", visited.Take(5)) +
                     (visited.Count > 5 ? $", ... ({visited.Count - 5} more)" : ""));

            if (visited.Distinct().Count() != visited.Count)
            {
                // two things with the same name is not a failure; landing twice on one IS
                var twice = visited.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);

                GD.Print($"        two or more things share a name: {string.Join(", ", twice)}");
            }

            string first = hand.On.Called;

            hand.Next();

            if (hand.Live > 1 && hand.On.Called == first)
                Problem("the hand does not come round again - the last thing in the room is a " +
                        "dead end");

            // and back the other way
            if (!hand.Back())
                Problem("the hand cannot reach backward, so anything you overshoot is unreachable");

            hand.Nothing();
        }


        // ---- the voice -------------------------------------------------------------------------

        void NothingSaidIsAKey(Reaching reach, RoomNode room)
        {
            Narrator reader = reach.Reader;

            GD.Print("");

            if (reader == null)
            {
                Problem("there is no narrator in the room, so nothing can be read out at all");
                return;
            }

            if (!reader.Able)
                Caution("this machine has no text-to-speech voice, so nothing was actually spoken " +
                        "- what would have been said was still counted, and a person with a voice " +
                        "has to do the listening");

            reader.Forget();

            // the reader is switched on and the room is walked, exactly as a blind player would
            reader.Listening(true);

            Pointing hand = reach.Hand;

            hand.Nothing();

            int said = 0;

            for (int step = 0; step < hand.Live; step++)
            {
                if (!hand.Next()) break;

                Spoken what = Spoken.Reading(hand.On);

                if (!what.Any)
                {
                    Problem($"the hand is on '{hand.On.Called}' and there is nothing to say about " +
                            "it - a stop that says nothing is a stop a blind player cannot tell " +
                            "they are at");
                    continue;
                }

                foreach (string key in what.Keys())
                    Problem($"the narrator was given '{key}', which is a key and not words");

                said++;
            }

            GD.Print($"read    {said} stop(s) said something, and {reader.Everything.Count} " +
                     "line(s) went to the voice");

            if (said == 0) Problem("walking the whole room said nothing anywhere");

            foreach (string line in reader.Everything.Take(4)) GD.Print($"        \"{line}\"");

            reader.Listening(false);

            hand.Nothing();
        }


        // ---- the keys --------------------------------------------------------------------------

        void EveryActHasAKey(Reaching reach)
        {
            Bindings keys = reach.How.Keys;

            GD.Print("");
            GD.Print($"keys    {keys}");

            foreach (Act act in Enum.GetValues<Act>())
            {
                Bindings.Bound bound = keys.Of(act);

                if (!bound.Any)
                {
                    if (act.Essential())
                        Problem($"{act.Word()} has no key, and without it a keyboard player cannot " +
                                "get anywhere in the room at all");

                    continue;
                }

                // the InputMap is what the running game actually reads
                if (!InputMap.HasAction(act.Word()))
                {
                    Problem($"{act.Word()} is bound to {bound} and is not in the InputMap, so the " +
                            "key does nothing");
                    continue;
                }

                if (InputMap.ActionGetEvents(act.Word()).Count == 0)
                    Problem($"{act.Word()} is in the InputMap with no key on it");

                if (keys.Clash(bound, act) is { } clash)
                    Problem($"{act.Word()} and {clash.Word()} are both on {bound}");
            }

            // and rebinding holds, through the same arm-then-press the page uses
            keys.Arm(Act.Help);

            if (keys.Pressed(Key.Space) != Bindings.Took.Taken)
                Problem("a key another act already has was taken anyway, so one of the two acts is " +
                        "now unreachable");

            if (keys.Pressed(Key.F9) != Bindings.Took.Bound)
                Problem("a free key was refused, so nothing can be rebound");

            if (keys.Of(Act.Help).Key != Key.F9)
                Problem("a key was bound and the binding did not take");

            keys.Bind(Act.Help, Act.Help.Standard(), Act.Help.Shifted());
        }


        // ---- the dials -------------------------------------------------------------------------

        void TheDialsTurn(Reaching reach, RoomNode room)
        {
            GD.Print("");

            // the page the dials are printed on, opened the way a player opens it
            room.OpenTheBook(Tome.Campaign);

            if (!room.TurnTo(Page.Settings))
                Problem("the settings page cannot be turned to");

            foreach (Setting setting in Enum.GetValues<Setting>())
            {
                if (!_text.Has(setting.NameKey()))
                {
                    Problem($"the {setting.Word()} line has no words in this locale " +
                            $"({setting.NameKey()})");
                    continue;
                }

                if (!setting.Built())
                {
                    Caution($"the {setting.Word()} line is printed and does not turn yet");
                    continue;
                }

                string was = setting.Value(reach.How);

                foreach (string value in setting.Choices())
                    if (!_text.Has(setting.ValueKey(value)))
                        Problem($"{setting.Word()} can be set to '{value}' and that has no words " +
                                $"in this locale ({setting.ValueKey(value)})");

                if (!reach.Turned(setting))
                {
                    Problem($"the {setting.Word()} line did not turn");
                    continue;
                }

                if (setting.Value(reach.How) == was)
                    Problem($"the {setting.Word()} line turned and is set to the same thing");

                GD.Print($"dial    {_text.Format(setting.NameKey(), _text.Get(setting.ValueKey(setting.Value(reach.How))))}" +
                         $" - was {was}");
            }

            // WRITTEN DOWN AND STILL TRUE. A settings page that forgets is worse than none, because
            // the player sets it once and then believes it
            Adjustments back = Adjustments.From(reach.Folder());

            foreach (Setting setting in Enum.GetValues<Setting>())
            {
                if (!setting.Built()) continue;

                if (setting.Value(back) != setting.Value(reach.How))
                    Problem($"{setting.Word()} was set to {setting.Value(reach.How)} and reads " +
                            $"back as {setting.Value(back)} - what the player chose did not survive");
            }

            GD.Print($"kept    {back}");

            // AND WHAT IS ACTUALLY PRINTED ON THE PAGE. A line whose value never got counted in
            // shows the player a literal {0}, which is the one failure that looks like a translation
            // choice rather than a bug
            room.TurnTo(Page.Settings);

            NothingUnfilled(room, "the settings page");

            // the keys page is the other half of the book's meta pages
            if (!room.TurnTo(Page.Keys))
                Problem("the keys page cannot be turned to");

            NothingUnfilled(room, "the keys page");

            room.ShutTheBook();
        }

        void NothingUnfilled(RoomNode room, string page)
        {
            var open = room.GetNodeOrNull<Opened>("Book");

            if (open == null || !open.Showing)
            {
                Problem($"{page} was turned to and there is no open book showing it");
                return;
            }

            foreach (string line in open.Written)
            {
                if (line.Contains("{0}"))
                    Problem($"{page} reads \"{line}\" - the value was never counted in, and a " +
                            "literal placeholder on the page reads as a translation choice");

                if (Spoken.IsAKey(line))
                    Problem($"{page} reads \"{line}\", which is a key and not words");
            }

            GD.Print($"page    {page}: {string.Join(" / ", open.Written)}");
        }


        // ---- the letters -----------------------------------------------------------------------

        void TheLettersResize(Reaching reach, RoomNode room)
        {
            GD.Print("");

            var labels = Legible.Words(room).ToList();

            if (labels.Count == 0)
            {
                Problem("there are no words anywhere in this room, so nothing can be resized");
                return;
            }

            reach.How.Lettering = Lettering.Normal;
            reach.Applied();

            var authored = labels.ToDictionary(l => l.GetInstanceId(), l => l.FontSize);

            reach.How.Lettering = Lettering.Largest;
            reach.Applied();

            int bigger = labels.Count(l => l.FontSize > authored[l.GetInstanceId()]);

            // a label already at one point cannot get bigger by a third and stay an integer; most of
            // them are 24 or more
            if (bigger < labels.Count / 2)
                Problem($"the text size was turned up and only {bigger} of {labels.Count} label(s) " +
                        "grew");

            reach.How.Lettering = Lettering.Small;
            reach.Applied();

            int smaller = labels.Count(l => l.FontSize < authored[l.GetInstanceId()]);

            // AND BACK, EXACTLY. Scaling from the current size rather than the authored one
            // compounds, and three visits to the settings page leave the table unreadable
            reach.How.Lettering = Lettering.Normal;
            reach.Applied();

            int wrong = labels.Count(l => l.FontSize != authored[l.GetInstanceId()]);

            if (wrong > 0)
                Problem($"{wrong} label(s) did not go back to the size they were authored at, so " +
                        "turning the dial twice is not the same as not turning it");

            GD.Print($"letters {labels.Count} label(s): {bigger} grew, {smaller} shrank, and all " +
                     "of them came back");

            // high contrast changes the ink, and puts it back. Switched OFF before the ink is
            // written down, because turning every dial on the settings page turned this one on
            reach.How.HighContrast = false;
            reach.Applied();

            var ink = labels.ToDictionary(l => l.GetInstanceId(), l => l.Modulate);

            reach.How.HighContrast = true;
            reach.Applied();

            int hardened = labels.Count(l => l.Modulate != ink[l.GetInstanceId()] ||
                                             l.OutlineSize >= Legible.Edge);

            if (hardened == 0)
                Problem("high contrast was switched on and no word on the table changed");

            reach.How.HighContrast = false;
            reach.Applied();

            if (labels.Any(l => l.Modulate != ink[l.GetInstanceId()]))
                Problem("high contrast was switched off and the ink did not go back");

            GD.Print($"        high contrast hardened {hardened} of them, and let go again");
        }


        // ---- nothing in colour alone -----------------------------------------------------------

        void NoMeaningInColourAlone()
        {
            GD.Print("");
            GD.Print($"cues    {Enum.GetValues<Cue>().Length} coloured cue(s) in the game");

            foreach (KeyValuePair<Cue, Twin> one in Redundancies.All)
            {
                if (one.Value == Twin.None)
                {
                    Problem($"{one.Key.Word()} is carried by colour alone - {one.Key.How()}");
                    continue;
                }

                GD.Print($"        {one.Key.Word(),-14} {one.Value.ToString().ToLowerInvariant(),-9}" +
                         $" {one.Key.How()}");
            }
        }


        // ---- the sounds, in words --------------------------------------------------------------

        void TheSoundsHaveWords(Reaching reach)
        {
            GD.Print("");

            Captioned captions = reach.Captions;

            if (captions == null)
            {
                Problem("there is no caption card in the room, so nothing that is heard can be read");
                return;
            }

            reach.How.Captions = false;
            reach.Applied();

            if (captions.Says(Game.Audio.Sound.Dice))
                Problem("captions are off and a caption was shown anyway");

            reach.How.Captions = true;
            reach.Applied();

            captions.Forget();

            foreach (Game.Audio.Sound sound in Enum.GetValues<Game.Audio.Sound>())
            {
                if (!_text.Has(sound.CaptionKey()))
                {
                    Problem($"the {sound.Word()} sound has no caption in this locale " +
                            $"({sound.CaptionKey()})");
                    continue;
                }

                if (!captions.Says(sound))
                {
                    Problem($"the {sound.Word()} sound was made and no caption was shown");
                    continue;
                }

                GD.Print($"caption {sound.Word(),-10} \"{captions.Said}\"" +
                         (sound.Recorded() ? "" : "   (nothing recorded for it yet)"));

                if (!sound.Recorded())
                    Caution($"there are no recordings in {sound.Folder()}, so the {sound.Word()} " +
                            "caption describes a silence until somebody sits down with a microphone");
            }

            // and the speech speed reaches the caption's own dwell, not only the companion's bubbles
            reach.How.Speech = Pace.Slow;

            double slow = reach.How.Time("[dice clatter across the wood]");

            reach.How.Speech = Pace.Quick;

            if (reach.How.Time("[dice clatter across the wood]") >= slow)
                Problem("the speech speed does not change how long a line stays up");

            reach.How.Speech = Pace.Normal;
            reach.How.Captions = false;
            reach.Applied();
        }


        // ---- your arms, and where we are -------------------------------------------------------

        void YourArms(Reaching reach)
        {
            GD.Print("");

            Hands hands = reach.TheHands;

            if (hands == null)
            {
                Problem("there are no hands on your side of the table, so the table has the DM's " +
                        "presence in it and none of yours");
                return;
            }

            foreach (Arm arm in Enum.GetValues<Arm>())
            {
                hands.Wears(arm, Shown.Both);

                if (hands.Skin != arm) Problem($"the arms would not go {arm.Word()}");
            }

            hands.Wears(Arm.Goblin, Shown.Hidden);

            if (hands.Showing.Count() != 0)
                Problem("the arms were hidden and are still there");

            hands.Wears(Arm.Almond, Shown.Left);

            if (hands.Showing.Count() != 1)
                Problem("one arm was asked for and both or neither are there");

            hands.Wears(reach.How);

            GD.Print($"arms    {hands} - {Enum.GetValues<Arm>().Count(a => a.IsHuman())} human " +
                     $"tone(s) and {Enum.GetValues<Arm>().Count(a => !a.IsHuman())} that are " +
                     "plainly nobody's");
        }

        void WhereAreWe(RoomNode room)
        {
            GD.Print("");

            // nothing on the table: a cold room, which is the state this is most likely to be asked in
            Spoken cold = room.Whereabouts();

            if (!cold.Any)
                Problem("asked where we are with nothing on the table, the room said nothing at all");

            // and then with somewhere to be
            room.Where = () => "quest.saltmarch.the_quay.name";

            Spoken said = room.Whereabouts();

            foreach (string key in said.Keys())
                Problem($"the answer to \"where are we\" contains '{key}', which is a key");

            foreach (string line in said.Lines)
            {
                GD.Print($"orient  \"{line}\"");

                // held to the same rule check-voice holds every ui.* string to: this is the game
                // talking straight to you, so there is no third party in it to be a he
                foreach (string word in Game.Localization.SecondPerson.Faults(Whereabouts.Here, line))
                    Problem($"\"{line}\" - {Game.Localization.SecondPerson.Explain(Whereabouts.Here, word)}");
            }

            if (said.Lines.Count < 2)
                Problem("the answer to \"where are we\" does not say where you are and point at " +
                        "the log");

            room.Where = null;
        }


        // ---- the hour of the day ---------------------------------------------------------------

        void TheHourOfTheDay()
        {
            GD.Print("");

            var winter = new DateTime(2026, 12, 21, 23, 30, 0);
            var summer = new DateTime(2026, 6, 21, 13, 0, 0);

            Game.Room.Daylight night = Game.Room.Daylight.At(winter);
            Game.Room.Daylight noon = Game.Room.Daylight.At(summer);

            GD.Print($"hour    {winter:yyyy-MM-dd HH:mm} - {night}");
            GD.Print($"        {summer:yyyy-MM-dd HH:mm} - {noon}");

            if (!night.IsDark) Problem("midnight in December is not a dark room");

            if (noon.IsDark) Problem("one in the afternoon in June is a dark room");

            if (noon.Ambient <= night.Ambient)
                Problem("the room is no brighter at noon than at midnight");

            if (night.Lamp <= noon.Lamp)
                Problem("the lamp is no more use at midnight than at noon");

            // and a summer evening is not a winter evening, which is the whole reason the date is in it
            Game.Room.Daylight june = Game.Room.Daylight.At(new DateTime(2026, 6, 21, 21, 0, 0));
            Game.Room.Daylight december = Game.Room.Daylight.At(new DateTime(2026, 12, 21, 21, 0, 0));

            if (june.IsDark || !december.IsDark)
                Problem("nine in the evening is the same room in June and in December, so the " +
                        "calendar is not being read");
        }
    }
}

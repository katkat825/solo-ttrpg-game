using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Yarn;

namespace Content.Dialogue
{
    // One conversation, running. Godot-free on purpose: the branching is a rules-shaped question
    // and the table is only what draws it, so this is testable headless and the scene that shows
    // it holds no logic worth testing.
    //
    // Nothing here ever returns the .yarn source. A line comes out as a KEY and the presentation
    // resolves it - which is the whole reason the pseudolocale can prove the words came through
    // the localizer and not out of the campaign's dialogue folder.
    public sealed class Conversation
    {
        readonly DialogueBook _book;

        readonly Yarn.Dialogue _dialogue;

        readonly List<Said> _heard = new List<Said>();

        // exactly one of these is set when the machine stops; both null means it is over
        Said _saying;

        List<Choice> _choosing;

        bool _stopped;

        bool _answered;

        public Conversation(DialogueBook book, IVariableStorage storage = null)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));

            if (book.Program == null)
                throw new ArgumentException("This dialogue book did not compile, so nothing in it can be played.",
                                            nameof(book));

            _dialogue = new Yarn.Dialogue(storage ?? new MemoryVariableStore());
            _dialogue.SetProgram(book.Program);

            _dialogue.LineHandler = OnLine;
            _dialogue.OptionsHandler = OnOptions;
            _dialogue.CommandHandler = OnCommand;
            _dialogue.NodeCompleteHandler = _ => { };
            _dialogue.DialogueCompleteHandler = () => _stopped = true;

            // Yarn writes to the console otherwise, and a campaign is not allowed to print
            _dialogue.LogDebugMessage = _ => { };
            _dialogue.LogErrorMessage = message => Complained.Add(message);
        }

        // developer diagnostics from the runtime itself, not localized and never shown
        public IList<string> Complained { get; } = new List<string>();

        public string Node => _dialogue.CurrentNode;

        // the line waiting to be read, or null while a choice is open or the talk is over
        public Said Saying => _saying;

        public IReadOnlyList<Choice> Choosing =>
            (IReadOnlyList<Choice>)_choosing ?? Array.Empty<Choice>();

        public bool IsChoosing => _choosing != null;

        public bool IsOver => _stopped;

        // everything said this conversation, in order; what a transcript and a headless check read
        public IReadOnlyList<Said> Heard => _heard;

        // a command the campaign wrote that the engine does not know; named, never run
        public IList<string> Commands { get; } = new List<string>();


        public bool Start(string node)
        {
            if (!_book.Has(node)) return false;

            _heard.Clear();
            _stopped = false;
            _saying = null;
            _choosing = null;

            _dialogue.SetNode(node);

            return Advance();
        }

        // true while there is more to read; false once the conversation has ended
        public bool Advance()
        {
            if (_stopped) return false;

            _saying = null;
            _choosing = null;
            _answered = false;

            try
            {
                _dialogue.Continue();
            }
            catch (Exception threw)
            {
                // a campaign cannot be allowed to take the table down with it
                Complained.Add(threw.Message);
                _stopped = true;
                return false;
            }

            return !_stopped;
        }

        public bool Choose(int option)
        {
            if (_choosing == null || option < 0 || option >= _choosing.Count) return false;

            if (!_choosing[option].Offered) return false;

            _dialogue.SetSelectedOption(_choosing[option].Index);
            _answered = true;

            return Advance();
        }

        // whether the last choice was answered; a caller that advances without choosing gets nowhere
        public bool Answered => _answered;


        void OnLine(Line line)
        {
            DialogueLine known = _book.Line(line.ID);

            // a line with no key was refused at load and named there; it is skipped, not shown
            if (known == null) return;

            _saying = new Said(known, line.Substitutions ?? Array.Empty<string>());
            _heard.Add(_saying);
        }

        void OnOptions(OptionSet options)
        {
            var open = new List<Choice>();

            foreach (OptionSet.Option option in options.Options)
            {
                DialogueLine known = _book.Line(option.Line.ID);

                if (known == null) continue;

                open.Add(new Choice(option.ID, known,
                                    option.Line.Substitutions ?? Array.Empty<string>(),
                                    option.IsAvailable));
            }

            _choosing = open;
        }

        // content is data, never code (CONVENTIONS section 2). A << >> the engine does not itself
        // define is recorded and stepped over - there is deliberately no hook by which a campaign
        // could make one mean something, because on a storefront that is a security boundary.
        void OnCommand(Command command)
        {
            Commands.Add(command.Text);
        }


        // a line, ready to be put through a localizer
        public sealed class Said
        {
            public Said(DialogueLine line, IReadOnlyList<string> substitutions)
            {
                Line = line;
                Substitutions = substitutions ?? Array.Empty<string>();
            }

            public DialogueLine Line { get; }

            public string Key => Line.Key;

            public string Speaker => Line.Speaker;

            // Yarn's {0} interpolations, already rendered to strings by the runtime
            public IReadOnlyList<string> Substitutions { get; }

            // the one place a dialogue key becomes text, and it is the caller's localizer that does it
            public string Text(ILocalizer text)
            {
                if (text == null) throw new ArgumentNullException(nameof(text));

                return Substitutions.Count == 0
                    ? text.Get(Key)
                    : text.Format(Key, Substitutions.Cast<object>().ToArray());
            }

            public override string ToString() => Key;
        }

        public sealed class Choice
        {
            public Choice(int index, DialogueLine line, IReadOnlyList<string> substitutions,
                          bool offered)
            {
                Index = index;
                Line = line;
                Substitutions = substitutions ?? Array.Empty<string>();
                Offered = offered;
            }

            // Yarn's own option number, which is what SetSelectedOption wants
            public int Index { get; }

            public DialogueLine Line { get; }

            public string Key => Line.Key;

            public IReadOnlyList<string> Substitutions { get; }

            // a choice whose condition failed: shown greyed, never taken
            public bool Offered { get; }

            public string Text(ILocalizer text) =>
                new Said(Line, Substitutions).Text(text);

            public override string ToString() => $"{Index}: {Key}" + (Offered ? "" : " (closed)");
        }
    }
}

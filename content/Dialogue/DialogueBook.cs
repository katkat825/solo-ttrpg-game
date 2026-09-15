using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;
using Core.Localization;
using Yarn;
using Yarn.Compiler;

namespace Content.Dialogue
{
    // A campaign's dialogue/ folder, compiled.
    //
    // ARCHITECTURE.md section 6 is a standing decision and this is where it is honoured: the
    // branching runtime is Yarn Spinner, not ours. What IS ours is the seam between the two halves
    // of a line - Yarn owns structure and branching, the campaign's locale/ owns the words - which
    // is what keeps "translate the personality, not the wording" true and keeps game/locale/ free
    // of dialogue.* keys.
    //
    // Every line therefore needs an explicit #line: tag. Yarn will happily invent one (line:34f088b7,
    // a hash of the text), and an invented id is an unstable key: edit the line and the hash moves,
    // which silently orphans every translation of it. So an untagged line is refused by name here,
    // and the tag the author writes is the last segment of the key.
    public sealed class DialogueBook
    {
        public const string Extension = ".yarn";

        // a node says who is talking; a line may override it where two creatures share a scene
        public const string SpeakerHeader = "speaker";

        // a node with one of these is a camp scene, offered on a night that matches
        public const string TopicHeader = "topic";

        // Yarn's own, and not ours to set
        public const string TitleHeader = "title";

        readonly Dictionary<string, DialogueLine> _lines =
            new Dictionary<string, DialogueLine>(StringComparer.Ordinal);

        readonly Dictionary<string, string> _speakers =
            new Dictionary<string, string>(StringComparer.Ordinal);

        readonly Dictionary<string, Topic> _topics =
            new Dictionary<string, Topic>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        DialogueBook(string campaign) => Campaign = campaign ?? "";

        public string Campaign { get; }

        // null when nothing compiled; a campaign with no dialogue/ is normal, not broken
        public Program Program { get; private set; }

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public IReadOnlyCollection<string> Nodes => _speakers.Keys;

        public bool Has(string node) => node != null && _speakers.ContainsKey(node);

        public string SpeakerOf(string node) =>
            node != null && _speakers.TryGetValue(node, out string speaker) ? speaker : null;

        public Topic? TopicOf(string node) =>
            node != null && _topics.TryGetValue(node, out Topic topic) ? topic : (Topic?)null;

        public IEnumerable<string> CampNodes =>
            _topics.Keys.OrderBy(n => n, StringComparer.Ordinal);

        public DialogueLine Line(string yarnId) =>
            yarnId != null && _lines.TryGetValue(yarnId, out DialogueLine line) ? line : null;

        public IEnumerable<DialogueLine> Lines =>
            _lines.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _lines[i]);

        public IEnumerable<string> Speakers =>
            _lines.Values.Select(l => l.Speaker).Distinct().OrderBy(s => s, StringComparer.Ordinal);

        public IEnumerable<string> Keys() => Lines.Select(l => l.Key);

        public int Count => _lines.Count;


        public static DialogueBook Read(string folder, string campaign)
        {
            var book = new DialogueBook(campaign);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return book;

            var sources = new List<CompilationJob.File>();

            foreach (string path in Files(folder))
            {
                string name = Package.DialogueFolder + "/" + Path.GetFileName(path);

                try
                {
                    sources.Add(new CompilationJob.File { FileName = name, Source = File.ReadAllText(path) });
                }
                catch (Exception could)
                {
                    book._problems.Add(new ContentProblem(name, "", "could not be read - " + could.Message));
                }
            }

            if (sources.Count == 0) return book;

            book.Compile(sources);

            return book;
        }

        // exposed so a test can hand the compiler text without a folder; the game always uses Read
        public static DialogueBook Of(string campaign, params (string File, string Source)[] sources)
        {
            var book = new DialogueBook(campaign);

            book.Compile(sources
                .Select(s => new CompilationJob.File { FileName = s.File, Source = s.Source })
                .ToList());

            return book;
        }

        void Compile(IReadOnlyList<CompilationJob.File> sources)
        {
            CompilationResult result;

            // the compiler is a third party at the edge of the loader, and Workshop content is the
            // normal case for reaching a path it did not expect - so it is wrapped, not trusted
            try
            {
                result = Compiler.Compile(new CompilationJob
                {
                    Inputs = sources,
                    Library = new Library(),
                });
            }
            catch (Exception threw)
            {
                _problems.Add(new ContentProblem(
                    sources[0].FileName, "",
                    "the dialogue compiler could not read this folder - " + threw.Message));
                return;
            }

            bool fatal = false;

            foreach (Diagnostic complaint in result.Diagnostics)
            {
                // both are refused: Yarn calls a jump to a node nobody wrote a warning, and a
                // conversation that runs off its own end is as broken as one that will not compile
                _problems.Add(new ContentProblem(
                    complaint.FileName ?? "", "", complaint.Message, complaint.Range.Start.Line + 1));

                if (complaint.Severity == Diagnostic.DiagnosticSeverity.Error) fatal = true;
            }

            if (fatal || result.Program == null) return;

            // where each node's lines were written, so a header problem can point at a file; built
            // before the nodes because the compiler records a file per string and not per node
            var where = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (StringInfo info in result.StringTable.Values)
                if (info.nodeName != null && !where.ContainsKey(info.nodeName))
                    where[info.nodeName] = info.fileName ?? Package.DialogueFolder;

            ReadNodes(result.Program, where);
            ReadLines(result.StringTable);

            // the program is kept even where a line was refused: the refusal names the line, and a
            // half-keyed conversation still branches, so the author can play what they have written
            Program = result.Program;
        }

        void ReadNodes(Program program, IReadOnlyDictionary<string, string> where)
        {
            foreach (KeyValuePair<string, Node> node in program.Nodes)
            {
                string speaker = Header(node.Value, SpeakerHeader);
                string file = where.TryGetValue(node.Key, out string named) ? named
                                                                            : Package.DialogueFolder;

                if (speaker == null)
                {
                    _problems.Add(new ContentProblem(
                        file, node.Key,
                        $"node '{node.Key}' has no '{SpeakerHeader}:' header, so there is nobody " +
                        "to key its lines under - add one naming the creature that talks " +
                        "('speaker: wolf', never 'speaker: barbarian')"));
                    continue;
                }

                if (!ContentId.IsLocal(speaker))
                {
                    _problems.Add(new ContentProblem(
                        file, node.Key + "." + SpeakerHeader,
                        $"'{speaker}' is not a creature id - lowercase a-z, 0-9 and underscore"));
                    continue;
                }

                _speakers[node.Key] = speaker;

                string topic = Header(node.Value, TopicHeader);

                if (topic == null) continue;

                if (!Topics.TryWord(topic, out Topic about))
                {
                    _problems.Add(new ContentProblem(
                        file, node.Key + "." + TopicHeader,
                        $"'{topic}' is not something a night can be about - it is one of " +
                        $"{Vocabulary.Offer(Topics.Words)}. The list is the engine's, because the " +
                        "day is what picks the topic"));
                    continue;
                }

                _topics[node.Key] = about;
            }
        }

        void ReadLines(IDictionary<string, StringInfo> table)
        {
            foreach (KeyValuePair<string, StringInfo> entry in table)
            {
                StringInfo info = entry.Value;
                string file = info.fileName ?? "";

                if (info.isImplicitTag)
                {
                    _problems.Add(new ContentProblem(
                        file, "",
                        "this line has no '#line:' tag, so the only name it has is a hash of its " +
                        "own text - edit the words and every translation of it is orphaned. Give " +
                        "it one: '#line:the_hinges_are_new'", info.lineNumber));
                    continue;
                }

                string local = DialogueKeys.LocalOf(entry.Key);

                if (local == null || !ContentId.IsLocal(local))
                {
                    _problems.Add(new ContentProblem(
                        file, "",
                        $"'{entry.Key}' is not a line id this game can key - the part after " +
                        $"'{DialogueKeys.YarnPrefix}' is lowercase a-z, 0-9 and underscore, " +
                        "because it becomes the last segment of a localization key",
                        info.lineNumber));
                    continue;
                }

                // a line may name its own speaker where two creatures share a node
                string speaker = Spoken(info) ?? SpeakerOf(info.nodeName);

                if (speaker == null) continue;

                string key = DialogueKeys.Line(speaker, Campaign, local);

                if (!KeyConventions.IsWellFormed(key))
                {
                    _problems.Add(new ContentProblem(
                        file, "",
                        $"this line would be keyed '{key}', and {KeyConventions.Explain(key)}",
                        info.lineNumber));
                    continue;
                }

                _lines[entry.Key] = new DialogueLine(entry.Key, key, speaker, info.nodeName, file,
                                                     info.lineNumber, info.text);
            }
        }

        // a per-line '#speaker:imp' tag, for the lines in a node that somebody else says
        static string Spoken(StringInfo info)
        {
            foreach (string tag in info.metadata ?? Array.Empty<string>())
            {
                if (!tag.StartsWith(SpeakerHeader + ":", StringComparison.Ordinal)) continue;

                string named = tag.Substring(SpeakerHeader.Length + 1);

                return ContentId.IsLocal(named) ? named : null;
            }

            return null;
        }

        static string Header(Node node, string name)
        {
            foreach (Header header in node.Headers)
                if (string.Equals(header.Key, name, StringComparison.Ordinal))
                    return string.IsNullOrWhiteSpace(header.Value) ? null : header.Value.Trim();

            return null;
        }

        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        public override string ToString() =>
            $"{_speakers.Count} nodes, {_lines.Count} lines" +
            (_topics.Count > 0 ? $", {_topics.Count} camp scenes" : "") +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}

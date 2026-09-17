using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;
using Content.World;

namespace Content.Quests
{
    public sealed class QuestBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Quest> _quests =
            new Dictionary<string, Quest>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        public IReadOnlyCollection<string> Ids => _quests.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public int Count => _quests.Count;

        public bool Has(string id) => id != null && _quests.ContainsKey(id);

        public Quest Of(string id) =>
            id != null && _quests.TryGetValue(id, out Quest quest) ? quest : null;

        public IEnumerable<Quest> All =>
            _quests.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _quests[i]);

        public IEnumerable<string> Keys(string campaign) => All.SelectMany(q => q.Keys(campaign));

        // nothing is stored; asking again after a murder gives a different answer, by design
        public IEnumerable<(Quest Quest, QuestState State)> Log(Facts facts)
        {
            foreach (Quest quest in All)
            {
                QuestState state = quest.StateIn(facts);

                if (state.InTheLog()) yield return (quest, state);
            }
        }


        public static QuestBook Read(string folder)
        {
            var book = new QuestBook();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return book;

            foreach (string path in Directory
                         .EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                         .OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);
                string text;

                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception could)
                {
                    book._problems.Add(new ContentProblem(
                        name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<Quest> read = QuestReader.Parse(text, name);

                if (!read.Ok)
                {
                    book._problems.AddRange(read.Problems);
                    continue;
                }

                if (book._quests.ContainsKey(read.Value.Id))
                {
                    book._problems.Add(new ContentProblem(
                        name, "id",
                        $"'{read.Value.Id}' is already the id of another quest in this folder"));
                    continue;
                }

                book._quests[read.Value.Id] = read.Value;
            }

            return book;
        }

        public override string ToString() =>
            $"{_quests.Count} quests" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}

using System;
using System.Collections.Generic;

namespace Content.Schema
{
    public sealed class ContentProblem
    {
        public ContentProblem(string file, string where, string what, int line = 0)
        {
            File = file ?? "";
            Where = where ?? "";
            What = what ?? "";
            Line = line;
        }

        public string File { get; }

        // a JSON path like "attributes.might", or empty for the file itself
        public string Where { get; }

        public string What { get; }

        // 1-based, or 0 when the parser knew no line
        public int Line { get; }

        public override string ToString()
        {
            string at = Line > 0 && Where.Length > 0 ? $"line {Line}, {Where}"
                      : Line > 0 ? $"line {Line}"
                      : Where.Length > 0 ? Where
                      : "";

            return at.Length > 0 ? $"{File}: {at} - {What}" : $"{File}: {What}";
        }
    }

    // the thing, or the reasons it isn't; never both half-done, never an exception
    public sealed class Read<T> where T : class
    {
        readonly List<ContentProblem> _problems;

        Read(T value, List<ContentProblem> problems)
        {
            Value = value;
            _problems = problems ?? new List<ContentProblem>();
        }

        public T Value { get; }

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Ok => Value != null && _problems.Count == 0;

        public static Read<T> Good(T value) => new Read<T>(value, null);

        public static Read<T> Bad(IEnumerable<ContentProblem> problems) =>
            new Read<T>(null, new List<ContentProblem>(problems ?? Array.Empty<ContentProblem>()));

        public static Read<T> Bad(ContentProblem problem) => Bad(new[] { problem });

        // a value and a list; Ok is still false (it means "nothing was wrong", not "there is a value")
        public static Read<T> Partial(T value, IEnumerable<ContentProblem> problems) =>
            new Read<T>(value, new List<ContentProblem>(problems ?? Array.Empty<ContentProblem>()));

        // there is a value, understood or not; the question a save asks and a campaign never needs
        public bool Any => Value != null;

        public override string ToString() =>
            Ok ? $"read {typeof(T).Name}"
               : $"{_problems.Count} problems reading {typeof(T).Name}" +
                 (Any ? ", read as far as it could be" : "");
    }
}

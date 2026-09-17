using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;

namespace Content.World
{
    // two lists rather than one with a '!', so 'when' and 'unless' both read as english
    public sealed class Requirement
    {
        public static readonly Requirement Always = new Requirement(null, null);

        public Requirement(IReadOnlyList<string> when, IReadOnlyList<string> unless)
        {
            When = when ?? Array.Empty<string>();
            Unless = unless ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> When { get; }

        public IReadOnlyList<string> Unless { get; }

        public bool IsAlways => When.Count == 0 && Unless.Count == 0;

        public bool Met(Facts facts)
        {
            if (facts == null) return IsAlways;

            foreach (string fact in When)
                if (!facts.Is(fact)) return false;

            foreach (string fact in Unless)
                if (facts.Is(fact)) return false;

            return true;
        }

        // every fact this names, for the validator to check against what the campaign can set
        public IEnumerable<string> Facts => When.Concat(Unless);


        public static readonly IReadOnlyList<string> Fields = new[] { "when", "unless" };

        // reads the two fields off whatever it is handed; absent means Always, which is most
        public static Requirement Parse(JsonElement element, string file, string where,
                                        List<ContentProblem> problems)
        {
            IReadOnlyList<string> when = List(element, "when", file, where, problems);
            IReadOnlyList<string> unless = List(element, "unless", file, where, problems);

            if (when.Count == 0 && unless.Count == 0) return Always;

            foreach (string both in when.Intersect(unless, StringComparer.Ordinal))
                problems.Add(new ContentProblem(
                    file, Where(where, "when"),
                    $"'{both}' is in both 'when' and 'unless', so this can never be true - one of " +
                    "the two lists is the one you meant"));

            return new Requirement(when, unless);
        }

        static IReadOnlyList<string> List(JsonElement element, string field, string file,
                                          string where, List<ContentProblem> problems)
        {
            var facts = new List<string>();

            if (!element.TryGetProperty(field, out JsonElement value)) return facts;

            if (value.ValueKind == JsonValueKind.Null) return facts;

            // one fact is the common case, and a bare string is how anyone writes it
            if (value.ValueKind == JsonValueKind.String)
            {
                One(value.GetString(), file, Where(where, field), problems, facts);
                return facts;
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, Where(where, field),
                    $"'{field}' is a fact, or a list of them - [ \"bob.dead\" ]"));
                return facts;
            }

            int at = 0;

            foreach (JsonElement entry in value.EnumerateArray())
            {
                string spot = Where(where, field) + $"[{at++}]";

                if (entry.ValueKind != JsonValueKind.String)
                {
                    problems.Add(new ContentProblem(
                        file, spot, $"a fact is a name and this is a {Named(entry.ValueKind)}"));
                    continue;
                }

                One(entry.GetString(), file, spot, problems, facts);
            }

            return facts;
        }

        static void One(string name, string file, string spot, List<ContentProblem> problems,
                        List<string> facts)
        {
            string explained = FactName.Explain(name);

            if (explained != FactName.WellFormed)
            {
                problems.Add(new ContentProblem(file, spot, explained));
                return;
            }

            if (!facts.Contains(name, StringComparer.Ordinal)) facts.Add(name);
        }

        static string Where(string where, string field) =>
            string.IsNullOrEmpty(where) ? field : where + "." + field;

        static string Named(JsonValueKind kind) => kind.ToString().ToLowerInvariant();

        public override string ToString() =>
            IsAlways
                ? "always"
                : string.Join(" and ",
                    new[]
                    {
                        When.Count > 0 ? "when " + string.Join(", ", When) : null,
                        Unless.Count > 0 ? "unless " + string.Join(", ", Unless) : null,
                    }.Where(s => s != null));
    }
}

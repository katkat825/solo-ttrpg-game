using System;
using System.Collections.Generic;

namespace Content.Schema
{
    // ONE THING WRONG WITH ONE FILE, SAID THE WAY `MapReader` SAYS IT.
    //
    // "Refuse and name - name the file, the line, what is wrong." That discipline is the reason
    // B2's map format is pleasant to hand-write, and `CONTENT_PIPELINE.md` generalises it across
    // every schema: once players ship campaigns, a broken folder is the NORMAL case rather than a
    // developer bug, and the difference between a game that is moddable and one that is not is
    // almost entirely the quality of this sentence.
    //
    // A JSON PATH RATHER THAN A LINE, most of the time. `System.Text.Json` gives a line and a
    // column for a SYNTAX error - a missing brace - and nothing at all for a semantic one, because
    // by then the document has parsed and there is no text left to point at. So a problem carries
    // whichever it has: the line when the parser knew one, and always the path to the field
    // (`attributes.might`), which is what a person actually needs to find it.
    //
    // NOTHING HERE THROWS. A problem is a value that gets collected, because a validator that
    // stops at the first mistake turns authoring into a load-fix-load loop and one that reports
    // all of them turns it into a list to work through (`CONTENT_PIPELINE.md` P4).
    public sealed class ContentProblem
    {
        public ContentProblem(string file, string where, string what, int line = 0)
        {
            File = file ?? "";
            Where = where ?? "";
            What = what ?? "";
            Line = line;
        }

        // the file it is in, as the author would name it - a path relative to the campaign folder
        public string File { get; }

        // where in the file: a JSON path like "attributes.might", or empty for the file itself
        public string Where { get; }

        public string What { get; }

        // 1-based, or 0 when the parser did not know one
        public int Line { get; }

        // DEVELOPER AND AUTHOR FACING, and deliberately not localized. A campaign author reading
        // this is reading it in a console or a validator report, in the language the schema keys
        // are written in - which is the same argument that keeps `Actor.DebugName` unlocalized
        public override string ToString()
        {
            string at = Line > 0 && Where.Length > 0 ? $"line {Line}, {Where}"
                      : Line > 0 ? $"line {Line}"
                      : Where.Length > 0 ? Where
                      : "";

            return at.Length > 0 ? $"{File}: {at} - {What}" : $"{File}: {What}";
        }
    }

    // WHAT A READ CAME BACK WITH: the thing, or the reasons it is not the thing. Never both
    // half-done, and never an exception - a campaign that fails to load has to fail NAMED and
    // ALONE (`CONTENT_PIPELINE.md`, the isolation boundary), and an exception thrown out of a
    // loader is the opposite of both
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

        // A VALUE *AND* A LIST OF THINGS THAT WERE WRONG WITH IT, which is neither of the two
        // above and is exactly what a SAVE comes back as (CONTENT_PIPELINE.md P6).
        //
        // WHY CONTENT HAS TWO STATES AND A SAVE HAS THREE. A campaign is somebody else's and a
        // broken one should not be played - half a stranger's content is a game that plays wrong,
        // so `Good` or `Bad` is the whole of the question. A save is the player's own afternoon
        // and the worst thing that can be done with it is to decline to open it, so a field that
        // could not be read takes a default, says so, and the save loads.
        //
        // `Ok` IS STILL FALSE, deliberately - it means "nothing was wrong", not "there is a
        // value". A caller that wants the value asks for the value; a caller that wants to know
        // whether to warn asks `Ok`; and neither reading is the trap that a true `Ok` with a list
        // of problems attached would be
        public static Read<T> Partial(T value, IEnumerable<ContentProblem> problems) =>
            new Read<T>(value, new List<ContentProblem>(problems ?? Array.Empty<ContentProblem>()));

        // there IS something to work with, whether or not everything about it was understood -
        // the question a save asks and a campaign never needs to
        public bool Any => Value != null;

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            Ok ? $"read {typeof(T).Name}"
               : $"{_problems.Count} problems reading {typeof(T).Name}" +
                 (Any ? ", read as far as it could be" : "");
    }
}

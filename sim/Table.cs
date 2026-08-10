using System;

namespace Sim
{
    // fixed-width console table formatting
    // keeps padding noise out of the reports
    // console only - nothing here is player-facing, so none of it is localized
    static class Table
    {
        const int LabelWidth = 26;
        const int CellWidth = 13;

        public static void Title(string text)
        {
            Console.WriteLine();
            Console.WriteLine("=== " + text + " ===");
        }

        public static void Header(string label, params string[] columns)
        {
            Console.Write(label.PadRight(LabelWidth));
            foreach (var c in columns) Console.Write(c.PadLeft(CellWidth));
            Console.WriteLine();
        }

        public static void Row(string label, params string[] cells)
        {
            Console.Write(label.PadRight(LabelWidth));
            foreach (var c in cells) Console.Write(c.PadLeft(CellWidth));
            Console.WriteLine();
        }

        public static string Pct(double value) => value.ToString("F1") + "%";

        public static string Num(double value) => value.ToString("F1");
    }
}

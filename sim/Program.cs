using System;

namespace Sim
{
    // headless balance harness
    // there is no playtest group, so this stands in for one
    // re-run it after changing any number in the dice system
    // dotnet run --project sim [trials]
    static class Program
    {
        const int DefaultTrials = 20000;

        static void Main(string[] args)
        {
            int trials = args.Length > 0 && int.TryParse(args[0], out var t) ? t : DefaultTrials;

            DifficultyLadderReport.Run(trials);
            SnagReport.Run(trials);
            EncounterReport.ActionEconomy(trials);
            EncounterReport.RabbleSensitivity(trials);

            Console.WriteLine();
        }
    }
}

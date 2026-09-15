using System;

namespace Sim
{
    // headless balance harness; re-run after changing any dice-system number
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
            PlayerPathReport.Run(trials);
            TroubleReport.Run(trials);
            DreadReport.Run(trials);
            ClassReport.Run(trials);

            Console.WriteLine();
        }
    }
}

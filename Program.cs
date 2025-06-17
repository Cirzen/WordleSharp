using WordleSharp.Calculators;

namespace WordleSharp;

internal class Program
{
    static async Task Main(string[] args)
    {
        var wordle = new Wordle();
        wordle.SetNextWordCalculator(new CountReductionCalculator(4));
        wordle.DisplayCountOnly = false;
        var result = await wordle.Analyse();
        var columns = typeof(WordleResult).GetProperties().Select(info => info.Name);

        Console.WriteLine();
        WriteWordleResult(result);
            
    }

    private static void WriteWordleResult(WordleResult result)
    {
        var table = new ConsoleTables.ConsoleTable("StartWord", "Turns", "AttemptedWords", "Answer");
        table.AddRow(result.StartWord, result.Turns, result.AttemptedWords, result.Answer);
        table.Write(ConsoleTables.Format.Minimal);
    }

}
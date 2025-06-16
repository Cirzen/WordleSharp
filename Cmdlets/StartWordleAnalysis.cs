using System.Management.Automation;
using WordleSharp.Calculators;

namespace WordleSharp.Cmdlets;

[Cmdlet(VerbsLifecycle.Start,"WordleAnalysis")]
public class StartWordleAnalysis : PSCmdlet
{
    [Parameter()]
    public SwitchParameter CountOnly { get; set; }

    [Parameter()]
    [ValidateRange(1, int.MaxValue)]
    public int Threshold { get; set; } = 500;

    [Parameter()]
    [ValidateSet("CountReduction", "LetterFrequency")]
    public CalculatorType Calculator { get; set; } = CalculatorType.CountReduction;

    [Parameter()]
    [ValidateRange(1, int.MaxValue)]
    public int? MaxDegreeOfParallelism { get; set; }

    public StartWordleAnalysis()
    {

    }

    protected override void EndProcessing()
    {
        base.EndProcessing();
        var wordle = new Wordle(Threshold)
        {
            DisplayCountOnly = CountOnly.ToBool()
        };
        
        // Create the calculator using the factory
        var calculator = CalculatorFactory.CreateCalculator(Calculator, MaxDegreeOfParallelism);
        wordle.SetNextWordCalculator(calculator);
        
        var result = wordle.Analyse().GetAwaiter().GetResult();
        WriteObject(result);
        if (!CountOnly)
        {
            Console.WriteLine("Auto play");
            Console.Out.Flush();
            wordle.Reset();
            WriteObject(wordle.AutoPlay(result.StartWord, result.Answer));
            Console.WriteLine("Best start word(s)");
            Console.Out.Flush();
            wordle.Reset();
            WriteObject(wordle.GetBestStartWord(result.Answer));
        }
    }

}

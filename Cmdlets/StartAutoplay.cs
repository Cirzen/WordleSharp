using System.Management.Automation;
using WordleSharp.Calculators;
using WordleSharp.ProgressReporting;

namespace WordleSharp.Cmdlets;

[Cmdlet(VerbsLifecycle.Start, "Autoplay")]
public class StartAutoplay : PSCmdlet
{    [Parameter(Position = 0, Mandatory = true)]
    public required string StartWord { get; set; }

    [Parameter(Position = 1, Mandatory = true)]
    public required string Answer { get; set; }

    [Parameter()]
    [ValidateSet("CountReduction", "LetterFrequency")]
    public CalculatorType Calculator { get; set; } = CalculatorType.CountReduction;

    [Parameter()]
    [ValidateRange(1, int.MaxValue)]
    public int? MaxDegreeOfParallelism { get; set; }

    protected override void EndProcessing()
    {
        base.EndProcessing();
        var wordle = new Wordle();
        
        var calculator = CalculatorFactory.CreateCalculator(Calculator, MaxDegreeOfParallelism);
        wordle.SetNextWordCalculator(calculator);

        WriteObject(wordle.AutoPlay(StartWord, Answer));
    }
}

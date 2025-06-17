using System.Management.Automation;
using WordleSharp.Calculators;
using WordleSharp.ProgressReporting;

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

        var calculator = CalculatorFactory.CreateCalculator(Calculator, MaxDegreeOfParallelism);
        wordle.SetNextWordCalculator(calculator);
        using var progressUpdater = new PowerShellProgressUpdater(this, "Analyzing Wordle", 1);
        var analysisTask = wordle.Analyse(progressUpdater);

        var spinWait = new SpinWait();
        while (!analysisTask.IsCompleted)
        {
            progressUpdater.ProcessQueuedUpdates();
            spinWait.SpinOnce();
        }

        progressUpdater.ProcessQueuedUpdates();

        var result = analysisTask.GetAwaiter().GetResult();

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

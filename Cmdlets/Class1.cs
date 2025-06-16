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
        wordle.SetNextWordCalculator(new CountReductionCalculator());
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

[Cmdlet(VerbsCommon.Get, "WordScore")]
public class GetWordScore : PSCmdlet
{
    [Parameter(Position = 0, Mandatory = true)]
    public string Guess { get; set; }

    [Parameter(Position = 1, Mandatory = true)]
    public string Answer { get; set; }

    public GetWordScore()
    {

    }

    protected override void EndProcessing()
    {
        base.EndProcessing();
        WriteObject(Wordle.ScoreWord(Guess, Answer));
    }

}

[Cmdlet(VerbsLifecycle.Start, "Autoplay")]
public class StartAutoplay : PSCmdlet
{
    [Parameter(Position = 0, Mandatory = true)]
    public string StartWord { get; set; }

    [Parameter(Position = 1, Mandatory = true)]
    public string Answer { get; set; }

    protected override void EndProcessing()
    {
        base.EndProcessing();
        var wordle = new Wordle();
        wordle.SetNextWordCalculator(new CountReductionCalculator());

        WriteObject(wordle.AutoPlay(StartWord, Answer));
    }
}

[Cmdlet(VerbsCommon.Get, "BestStartWord")]
public class GetBestStartWord : PSCmdlet
{
    [Parameter(Position = 0, Mandatory = true)]
    public string Answer { get; set; }

    [Parameter(Position = 1)]
    public string[] StartWords { get; set; }

    protected override void EndProcessing()
    {
        base.EndProcessing();
        var wordle = new Wordle();
        wordle.SetNextWordCalculator(new CountReductionCalculator());

        WriteObject(wordle.GetBestStartWord(Answer, StartWords ?? wordle.startWords));
    }
}

[Cmdlet(VerbsCommon.Get, "WordsContainingLetter")]
public class GetWordsContainingLetter : PSCmdlet
{
    [Parameter(Mandatory = true)]
    public string Letter { get; set; }

    public GetWordsContainingLetter()
    {

    }

    protected override void EndProcessing()
    {
        base.EndProcessing();
        var wordle = new Wordle();
        WriteObject(wordle.GetWordsContainingLetter(Letter));
    }
}
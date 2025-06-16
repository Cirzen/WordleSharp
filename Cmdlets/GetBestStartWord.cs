using System.Management.Automation;
using WordleSharp.Calculators;

namespace WordleSharp.Cmdlets;

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

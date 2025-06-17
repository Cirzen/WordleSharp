using System.Management.Automation;

namespace WordleSharp.Cmdlets;

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

using System.Management.Automation;

namespace WordleSharp.Cmdlets;

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
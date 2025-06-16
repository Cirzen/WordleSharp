namespace WordleSharp.Calculators;

internal readonly struct ScoredLetter
{
    public char Character { get; }
    public LetterFeedback Feedback { get; }
    public int OriginalPositionInGuess { get; }

    public ScoredLetter(char character, LetterFeedback feedback, int originalPositionInGuess)
    {
        Character = character;
        Feedback = feedback;
        OriginalPositionInGuess = originalPositionInGuess;
    }
}
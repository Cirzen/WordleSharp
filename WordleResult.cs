﻿namespace WordleSharp;

/// <summary>
/// The result of a game played manually or automatically
/// </summary>
public class WordleResult
{
    public string StartWord;
    public int Turns;
    public AttemptedWords AttemptedWords;
    public string Answer;

    public WordleResult()
    {
    }

    public WordleResult(string answer, int turns, IEnumerable<string> attemptedWords)
    {
        StartWord = attemptedWords.FirstOrDefault();
        if (StartWord is null && turns == 1)
        {
            StartWord = answer;
        }
        Answer = answer;
        Turns = turns;
        AttemptedWords = new AttemptedWords(attemptedWords);
    }
}
using System.Collections.Concurrent;
using WordleSharp.ProgressReporting;

namespace WordleSharp.Calculators;

internal class CountReductionCalculator : INextWordCalculator
{
    private static readonly HashSet<char> AToZ = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'];
    private readonly int _maxDop;

    public CountReductionCalculator(int? maxDop = null)
    {
        _maxDop = maxDop ?? Environment.ProcessorCount;
    }
        
    public IEnumerable<string> CalculateWord(Wordle wordle)
    {
        return CalculateWordAsync(wordle, null).GetAwaiter().GetResult();
    }

    public async Task<IEnumerable<string>> CalculateWordAsync(Wordle wordle, IProgressUpdater? progressUpdater = null)
    {
        // Heuristic for very small filtered lists - return them directly if 1 or 2 words remain.
        if (wordle.filtered.Length > 0 && wordle.filtered.Length <= 2)
        {
            progressUpdater?.StartProgress(1, "Small list, returning directly.");
            progressUpdater?.IncrementProgress();
            progressUpdater?.CompleteProgress("Direct return complete.");
            return wordle.filtered.OrderBy(x => x);
        }

        string[] candidateGuesses = GetCandidateGuesses(wordle);
        if (!candidateGuesses.Any())
        {
            progressUpdater?.StartProgress(1, "No candidate guesses found.");
            progressUpdater?.IncrementProgress();
            progressUpdater?.CompleteProgress("No candidates, returning empty.");
            return Enumerable.Empty<string>();
        }

        FilteringCriteria baseCriteria = CreateBaseFilteringCriteria(wordle);
        string[] possibleAnswers = wordle.filtered.ToArray(); // These are the actual remaining possible answers

        // Pass the progressUpdater to ScoreCandidateGuessesAsync
        ConcurrentDictionary<string, double> wordScores = await ScoreCandidateGuessesAsync(
            candidateGuesses,
            possibleAnswers, 
            baseCriteria,
            _maxDop,
            new HashSet<string>(wordle.filtered), // Pass the original filtered list for the heuristic
            progressUpdater
        );

        return SelectBestWordsByScore(wordScores, possibleAnswers);
    }

    internal static string[] GetCandidateGuesses(Wordle wordle)
    {
        IEnumerable<string> initialWordsToConsider;
        if (wordle.filtered.Length != 0 && wordle.filtered.Length <= wordle.startWords.Count())
        {
            initialWordsToConsider = wordle.filtered.ToArray();
        }
        else
        {
            initialWordsToConsider = wordle.sortedWords;
        }
        return initialWordsToConsider.ToArray();
    }

    internal static FilteringCriteria CreateBaseFilteringCriteria(Wordle wordle)
    {
        var baseCriteria = new FilteringCriteria(
            (string[])wordle.regexArray.Clone(),
            new List<char>(wordle.globalExcluded),
            (string[])wordle.positionExcluded.Clone(),
            new List<char>() // MustBePresentChars will be populated next
        );

        // Populate MustBePresentChars from yellows (wordle.possibles)
        if (wordle.possibles != null)
        {
            foreach (var pStr in wordle.possibles) // pStr is like wordle.possibles[i]
            {
                if (!string.IsNullOrEmpty(pStr))
                {
                    foreach (char c in pStr)
                    {
                        if (char.IsLetter(c) && !baseCriteria.MustBePresentChars.Contains(c))
                        {
                            baseCriteria.MustBePresentChars.Add(c);
                        }
                    }
                }
            }
        }

        // Populate MustBePresentChars from greens (wordle.regexArray)
        // This ensures that even if a letter was globally excluded then later found as green,
        // it's correctly marked as must-be-present for the calculator's logic.
        for (int i = 0; i < 5; i++)
        {
            if (baseCriteria.RegexArray[i] != null && baseCriteria.RegexArray[i]!.Length == 1 && char.IsLetter(baseCriteria.RegexArray[i]![0]))
            {
                char greenChar = baseCriteria.RegexArray[i]![0];
                if (!baseCriteria.MustBePresentChars.Contains(greenChar))
                {
                    baseCriteria.MustBePresentChars.Add(greenChar);
                }
            }
        }
        return baseCriteria;
    }

    internal static async Task<ConcurrentDictionary<string, double>> ScoreCandidateGuessesAsync(
        string[] candidateGuesses,
        string[] possibleAnswers, // This is wordle.filtered effectively
        FilteringCriteria baseCriteria,
        int maxDop,
        IReadOnlyCollection<string> currentWordleFilteredForHeuristic, // Specifically for the heuristic check
        IProgressUpdater? progressUpdater = null // Added IProgressUpdater
    )
    {
        var wordScores = new ConcurrentDictionary<string, double>();
        var dop = Math.Min(Environment.ProcessorCount, maxDop);
        var options = new ParallelOptions { MaxDegreeOfParallelism = dop };

        // Initialize progress reporting
        progressUpdater?.StartProgress(candidateGuesses.Length, "Scoring candidate guesses...");
        int processedCount = 0;

        await Task.Run(() => Parallel.ForEach(candidateGuesses, options, guessToEvaluate =>
        {
            long totalRemainingWordsAfterThisGuess = 0;
            foreach (string hypotheticalAnswer in possibleAnswers) // Iterate over actual possible answers
            {
                totalRemainingWordsAfterThisGuess += ProcessHypotheticalInternal(
                    guessToEvaluate,
                    hypotheticalAnswer,
                    possibleAnswers, // Filter this list based on the outcome
                    baseCriteria
                );
            }

            if (possibleAnswers.Length > 0)
            {
                wordScores[guessToEvaluate] = (double)totalRemainingWordsAfterThisGuess / possibleAnswers.Length;
            }
            else
            {
                wordScores[guessToEvaluate] = double.MaxValue; // Should not happen if candidateGuesses.Any() and possibleAnswers.Any() initially
            }
                
            Interlocked.Increment(ref processedCount);
            progressUpdater?.UpdateProgress(processedCount, $"Scoring: {guessToEvaluate}");
        }));

        // Complete progress reporting
        progressUpdater?.CompleteProgress("Finished scoring all candidate guesses.");
        return wordScores;
    }

    internal static IEnumerable<string> SelectBestWordsByScore(ConcurrentDictionary<string, double> wordScores, string[] fallbackWords)
    {
        if (!wordScores.Any())
        {
            // Fallback: if no scores were computed, return the first from the original filtered list.
            return fallbackWords.Take(1);
        }

        var minScore = wordScores.Values.Min();
        return wordScores.Where(kvp => kvp.Value == minScore).Select(kvp => kvp.Key).OrderBy(x => x);
    }

    internal static ScoredLetter[] ScoreGuessAgainstAnswer(string guess, string hypotheticalAnswer)
    {
        var result = new ScoredLetter[5];
        char[] scoreChars = new char[5]; // g, y, -
        bool[] answerLetterUsed = new bool[5]; // To track usage of letters in hypotheticalAnswer for yellows

        // Greens
        for (int i = 0; i < 5; i++)
        {
            if (guess[i] == hypotheticalAnswer[i])
            {
                scoreChars[i] = 'g';
                answerLetterUsed[i] = true;
                result[i] = new ScoredLetter(guess[i], LetterFeedback.Green, i);
            }
        }

        // Yellows
        for (int i = 0; i < 5; i++)
        {
            if (scoreChars[i] == 'g')
            {
                continue; // Already processed as green
            }

            bool foundYellow = false;
            for (int j = 0; j < 5; j++)
            {
                if (guess[i] == hypotheticalAnswer[j] && !answerLetterUsed[j])
                {
                    scoreChars[i] = 'y';
                    answerLetterUsed[j] = true; // Mark this answer letter as used for yellow
                    result[i] = new ScoredLetter(guess[i], LetterFeedback.Yellow, i);
                    foundYellow = true;
                    break;
                }
            }
            if (!foundYellow) // If not green and not yellow, it's grey
            {
                scoreChars[i] = '-'; // Mark as grey for internal logic if needed, result already set
                result[i] = new ScoredLetter(guess[i], LetterFeedback.Grey, i);
            }
        }
        // Greys (ensure all letters have feedback)
        for (int i = 0; i < 5; i++)
        {
            if (scoreChars[i] == default(char)) // Should only be for letters not marked G or Y
            {
                result[i] = new ScoredLetter(guess[i], LetterFeedback.Grey, i);
            }
        }
        return result;
    }

    internal static void UpdateCriteriaFromScoredGuess(string guess, ScoredLetter[] scoredGuess, FilteringCriteria criteriaToUpdate)
    {
        // criteriaToUpdate is already a clone of the base criteria.
        // We modify it based on the scoredGuess.

        for (int i = 0; i < 5; i++)
        {
            ScoredLetter letterScore = scoredGuess[i];
            char guessedChar = letterScore.Character;

            switch (letterScore.Feedback)
            {
                case LetterFeedback.Green:
                    criteriaToUpdate.RegexArray[i] = guessedChar.ToString();
                    criteriaToUpdate.PositionExcluded[i] = null; // Green overrides positional exclusion for this spot
                    // If this char was globally excluded by a previous (different) grey letter, that's an issue.
                    // However, must-be-present should handle it. If 'a' is green, it must be present.
                    // If 'a' was globally excluded, Wordle rules usually mean that's a conflict or complex scenario.
                    // For now, green implies it's NOT globally excluded for the purpose of this word.
                    // And it must be present.
                    if (!criteriaToUpdate.MustBePresentChars.Contains(guessedChar))
                    {
                        criteriaToUpdate.MustBePresentChars.Add(guessedChar);
                    }
                    // Remove from global excluded if it was there due to a different letter position being grey
                    if (criteriaToUpdate.GlobalExcluded.Contains(guessedChar))
                    {
                        // This logic is complex: if 'A' in guess "APPLE" is green, but 'A' in "SALAD" (another guess) was grey.
                        // For the current guess "APPLE", 'A' is not globally excluded.
                        // The base globalExcluded should be from prior turns.
                        // If a letter is green, it cannot be globally excluded for future words.
                        // This implies baseCriteria should be cleaned if a green appears.
                        // This is usually handled by Wordle game logic before calling calculator.
                        // For this method, we assume baseCriteria is the true state *before* this guess's info.
                        // So, a green letter means it's *not* globally excluded *for words matching this guess's outcome*.
                        // This is subtle. Let's assume the primary effect is on RegexArray and MustBePresent.
                    }
                    break;

                case LetterFeedback.Yellow:
                    if (!criteriaToUpdate.MustBePresentChars.Contains(guessedChar))
                    {
                        criteriaToUpdate.MustBePresentChars.Add(guessedChar);
                    }
                    // Add to positional exclusion for the current spot i
                    if (criteriaToUpdate.PositionExcluded[i] == null)
                    {
                        criteriaToUpdate.PositionExcluded[i] = guessedChar.ToString();
                    }
                    else if (!criteriaToUpdate.PositionExcluded[i]!.Contains(guessedChar))
                    {
                        criteriaToUpdate.PositionExcluded[i] += guessedChar;
                    }
                    // Ensure the regex for this spot also excludes this yellow char
                    // This will be handled by the final regex build step.
                    // No direct change to RegexArray[i] here for yellows, it's derived later.
                    break;

                case LetterFeedback.Grey:
                    // Add to global excluded ONLY if this character is NOT green or yellow ANYWHERE in the current guess.
                    // This prevents adding 'A' to global excluded if guess is "APPLE" (A green, P grey)
                    // and the grey P is processed.
                    bool isGreenOrYellowElsewhereInGuess = false;
                    for(int j=0; j<5; j++)
                    {
                        if (scoredGuess[j].Character == guessedChar && 
                            (scoredGuess[j].Feedback == LetterFeedback.Green || scoredGuess[j].Feedback == LetterFeedback.Yellow))
                        {
                            isGreenOrYellowElsewhereInGuess = true;
                            break;
                        }
                    }
                    if (!isGreenOrYellowElsewhereInGuess && !criteriaToUpdate.GlobalExcluded.Contains(guessedChar))
                    {
                        criteriaToUpdate.GlobalExcluded.Add(guessedChar);
                    }
                    break;
            }
        }

        // Final step: Build the regex patterns for non-green slots based on all current criteria
        for (int i = 0; i < 5; i++)
        {
            if (criteriaToUpdate.RegexArray[i] != null && criteriaToUpdate.RegexArray[i]!.Length == 1 && char.IsLetter(criteriaToUpdate.RegexArray[i]![0]))
            {
                // This is a green letter, regex is already set (e.g., "a")
                continue;
            }

            var exclusionSetForPos = new SortedSet<char>();
            // Add all globally excluded characters
            foreach (var c in criteriaToUpdate.GlobalExcluded)
            {
                exclusionSetForPos.Add(c);
            }
            // Add characters positionally excluded from this specific spot (from yellows in this spot)
            if (criteriaToUpdate.PositionExcluded[i] != null)
            {
                foreach (var c in criteriaToUpdate.PositionExcluded[i]!) // Removed erroneous trailing '!' that was causing compile error
                {
                    exclusionSetForPos.Add(c);
                }
            }

            // Build the regex pattern for this position based on exclusions
            if (exclusionSetForPos.Count > 0)
            {
                // Create a character class for exclusion, e.g., "[^abc]"
                criteriaToUpdate.RegexArray[i] = "[^" + string.Join("", exclusionSetForPos) + "]";
            }
            else
            {
                criteriaToUpdate.RegexArray[i] = null; // No exclusions, regex is empty
            }
        }
    }

    internal static long ProcessHypotheticalInternal(
        string guess,
        string hypotheticalAnswer,
        string[] wordsToFilter,
        FilteringCriteria baseCriteria)
    {
        ScoredLetter[] scoredGuess = ScoreGuessAgainstAnswer(guess, hypotheticalAnswer);

        var derivedCriteria = new FilteringCriteria(baseCriteria);

        UpdateCriteriaFromScoredGuess(guess, scoredGuess, derivedCriteria);

        int count = 0;
        foreach (string word in wordsToFilter)
        {
            if (WordMatchesCriteriaManual(word, derivedCriteria))
            {
                count++;
            }
        }
        return count;
    }

    internal static bool WordMatchesCriteriaManual(string word, FilteringCriteria criteria) 
    {
        // Check 1: Regex match for fixed letters (greens) and position-specific exclusions (from yellows or greys)
        for (int i = 0; i < 5; i++)
        {
            string? patternPart = criteria.RegexArray[i];
            if (string.IsNullOrEmpty(patternPart) || patternPart == ".")
            {
                continue;
            }

            if (patternPart.Length == 1 && char.IsLetter(patternPart[0])) // Exact green letter
            {
                if (word[i] != patternPart[0])
                {
                    return false;
                }
            }
            else if (patternPart.StartsWith("[^") && patternPart.EndsWith("]")) // Exclusion class like "[^abc]"
            {
                for (int k = 2; k < patternPart.Length - 1; k++)
                {
                    if (word[i] == patternPart[k])
                    {
                        return false;
                    }
                }
            }
        }

        // Check 2: Global exclusions (greys)
        foreach (char globallyExcludedChar in criteria.GlobalExcluded)
        {
            if (word.Contains(globallyExcludedChar))
            {
                return false;
            }
        }
            
        // Check 3: Positional exclusions (explicitly marked for a spot, typically from yellows)
        for (int i = 0; i < 5; i++)
        {
            if (criteria.PositionExcluded[i] != null)
            {
                if (criteria.PositionExcluded[i]!.Contains(word[i]))
                {
                    return false;
                }
            }
        }

        // Check 4: Must-be-present letters (yellows)
        foreach (char mustBePresentChar in criteria.MustBePresentChars)
        {
            if (!word.Contains(mustBePresentChar))
            {
                return false;
            }
        }

        return true;
    }
}
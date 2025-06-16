using WordleSharp.Calculators;
using Xunit;
using System.Collections.Concurrent; // Added for ConcurrentDictionary

namespace WordleSharp.Tests;

public class CountReductionCalculatorTests
{
    private readonly CountReductionCalculator _calculator;

    public CountReductionCalculatorTests()
    {
        _calculator = new CountReductionCalculator();
    }

    [Theory]
    // Scenario 1: Simple Green Match
    [InlineData("apple", new string?[] { "a", null, null, "l", "e" }, new char[] { }, new string?[] { null, null, null, null, null }, new char[] { }, true)]
    // Scenario 2: Green Mismatch
    [InlineData("apply", new string?[] { "a", null, null, "l", "e" }, new char[] { }, new string?[] { null, null, null, null, null }, new char[] { }, false)]
    // Scenario 3a: Global Exclusion - Word does not contain excluded chars
    [InlineData("train", new string?[] { null, null, null, null, null }, new char[] { 's', 'o', 'u', 'p' }, new string?[] { null, null, null, null, null }, new char[] { }, true)]
    // Scenario 3b: Global Exclusion - Word contains an excluded char
    [InlineData("sound", new string?[] { null, null, null, null, null }, new char[] { 's', 'o', 'u', 'p' }, new string?[] { null, null, null, null, null }, new char[] { }, false)]
    // Scenario 4a: Positional Exclusion (Yellow) - Word respects positional exclusion (Corrected expected to false)
    [InlineData("slate", new string?[] { "[^s]", null, "[^a]", null, "e" }, new char[] { }, new string?[] { "s", null, "a", null, null }, new char[] { 's', 'l', 'a', 't' }, false)] // 'e' is green, 's', 'l', 'a', 't' are yellow. word[0] is 's', regex[0] is [^s] -> should be false.
    // Scenario 4b: Positional Exclusion (Yellow) - Word violates positional exclusion
    [InlineData("stale", new string?[] { "[^s]", null, "[^a]", null, "e" }, new char[] { }, new string?[] { "s", null, "a", null, null }, new char[] { 's', 't', 'a', 'l' }, false)] // 's' in pos 0, but excluded from pos 0
    // Scenario 5a: Must Be Present (Yellow) - Word contains all must-be-present chars
    [InlineData("crane", new string?[] { null, null, null, null, null }, new char[] { }, new string?[] { null, null, null, null, null }, new char[] { 'a', 'r' }, true)]
    // Scenario 5b: Must Be Present (Yellow) - Word misses a must-be-present char
    [InlineData("crony", new string?[] { null, null, null, null, null }, new char[] { }, new string?[] { null, null, null, null, null }, new char[] { 'a', 'r' }, false)]
    // Scenario 6a: Complex Case (Greens, Yellows, Greys) - Match (Corrected expected to false)
    [InlineData("react", new string?[] { null, "e", null, "[^c]", null }, new char[] { 's', 'o', 'u', 'n', 'd' }, new string?[] { null, null, null, "c", null }, new char[] { 'r', 'a', 'c', 't' }, false)] // word[3] is 'c', regex[3] is [^c] -> should be false
    // Scenario 6b: Complex Case - Violates positional exclusion for 'c'
    [InlineData("reach", new string?[] { null, "e", null, "[^c]", null }, new char[] { 's', 'o', 'u', 'n', 'd' }, new string?[] { null, null, null, "c", null }, new char[] { 'r', 'a', 'c', 't' }, false)]
    // Scenario 6c: Complex Case - Missing must-be-present 'a' and 'c' (even though 'c' is also positionally excluded)
    [InlineData("rebut", new string?[] { null, "e", null, "[^c]", null }, new char[] { 's', 'o', 'u', 'n', 'd' }, new string?[] { null, null, null, "c", null }, new char[] { 'r', 'a', 'c', 't' }, false)]
    // Scenario 7: Empty criteria (should match any 5-letter word)
    [InlineData("tests", new string?[] { null, null, null, null, null }, new char[] { }, new string?[] { null, null, null, null, null }, new char[] { }, true)]
    // Scenario 8: All positions fixed (green), word matches
    [InlineData("apple", new string?[] { "a", "p", "p", "l", "e" }, new char[] { }, new string?[] { null, null, null, null, null }, new char[] { }, true)]
    // Scenario 9: All positions fixed (green), word mismatches
    [InlineData("apply", new string?[] { "a", "p", "p", "l", "e" }, new char[] { }, new string?[] { null, null, null, null, null }, new char[] { }, false)]

    public void WordMatchesCriteriaManual_ReturnsExpected(string word, string?[] tempRegexArray, char[] globalExcludedChars, string?[] positionExcludedChars, char[] mustBePresentChars, bool expected)
    {
        var criteria = new FilteringCriteria
        {
            RegexArray = tempRegexArray,
            GlobalExcluded = globalExcludedChars.ToList(),
            PositionExcluded = positionExcludedChars,
            MustBePresentChars = mustBePresentChars.ToList()
        };
            
        var result = CountReductionCalculator.WordMatchesCriteriaManual(word, criteria); // Now static
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ProcessHypotheticalInternal_Scenario1_SimpleReduction()
    {
        string guess = "salet";
        string hypotheticalAnswer = "crane"; // s-, aY, l-, eY, t-
        string[] wordsToFilter = { "crane", "slate", "train", "grape", "frape", "dream" };
            
        var baseCriteria = new FilteringCriteria(initializeCollections: true); // Initialize with empty collections
        // baseCriteria.RegexArray will be all nulls
        // baseCriteria.GlobalExcluded will be empty
        // baseCriteria.PositionExcluded will be all nulls
        // baseCriteria.MustBePresentChars will be empty

        // The localTemp* variables are no longer part of the direct call or output of ProcessHypotheticalInternal
        // Their state is managed internally by UpdateCriteriaFromScoredGuess.
        // We will test UpdateCriteriaFromScoredGuess separately to verify this internal state.

        long count = CountReductionCalculator.ProcessHypotheticalInternal(
            guess, hypotheticalAnswer, wordsToFilter,
            baseCriteria
        );

        // The primary assertion for this test remains the count of matching words.
        // Detailed state assertions will be moved to tests for UpdateCriteriaFromScoredGuess.
        Assert.Equal(4, count);
    }

    [Theory]
    // Test cases for ScoreGuessAgainstAnswer
    // Guess, Answer, Expected ScoredLetters (Char, Feedback, OriginalPosition)
    [InlineData("apple", "apply", new object[] { 'a', LetterFeedback.Green, 0, 'p', LetterFeedback.Green, 1, 'p', LetterFeedback.Green, 2, 'l', LetterFeedback.Green, 3, 'e', LetterFeedback.Grey, 4 })]
    [InlineData("crane", "slate", new object[] { 'c', LetterFeedback.Grey, 0, 'r', LetterFeedback.Grey, 1, 'a', LetterFeedback.Green, 2, 'n', LetterFeedback.Grey, 3, 'e', LetterFeedback.Green, 4 })] // Corrected
    [InlineData("audio", "radio", new object[] { 'a', LetterFeedback.Yellow, 0, 'u', LetterFeedback.Grey, 1, 'd', LetterFeedback.Green, 2, 'i', LetterFeedback.Green, 3, 'o', LetterFeedback.Green, 4 })] // Corrected
    [InlineData("abbey", "babel", new object[] { 'a', LetterFeedback.Yellow, 0, 'b', LetterFeedback.Yellow, 1, 'b', LetterFeedback.Green, 2, 'e', LetterFeedback.Green, 3, 'y', LetterFeedback.Grey, 4 })] // Corrected
    [InlineData("speed", "pleas", new object[] { 's', LetterFeedback.Yellow, 0, 'p', LetterFeedback.Yellow, 1, 'e', LetterFeedback.Green, 2, 'e', LetterFeedback.Grey, 3, 'd', LetterFeedback.Grey, 4 })] // Corrected
    [InlineData("array", "radar", new object[] { 'a', LetterFeedback.Yellow, 0, 'r', LetterFeedback.Yellow, 1, 'r', LetterFeedback.Yellow, 2, 'a', LetterFeedback.Green, 3, 'y', LetterFeedback.Grey, 4 })] // Corrected
    [InlineData("tests", "tests", new object[] { 't', LetterFeedback.Green, 0, 'e', LetterFeedback.Green, 1, 's', LetterFeedback.Green, 2, 't', LetterFeedback.Green, 3, 's', LetterFeedback.Green, 4 })] // All green
    [InlineData("aaaaa", "bbbbb", new object[] { 'a', LetterFeedback.Grey, 0, 'a', LetterFeedback.Grey, 1, 'a', LetterFeedback.Grey, 2, 'a', LetterFeedback.Grey, 3, 'a', LetterFeedback.Grey, 4 })] // All grey
    public void ScoreGuessAgainstAnswer_ReturnsExpectedScores(string guess, string answer, object[] expectedScoredLettersRaw)
    {
        var expected = new List<ScoredLetter>();
        for (int i = 0; i < expectedScoredLettersRaw.Length; i += 3)
        {
            expected.Add(new ScoredLetter((char)expectedScoredLettersRaw[i], (LetterFeedback)expectedScoredLettersRaw[i+1], (int)expectedScoredLettersRaw[i+2]));
        }

        var actual = CountReductionCalculator.ScoreGuessAgainstAnswer(guess, answer);

        Assert.Equal(expected.Count, actual.Length);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Character, actual[i].Character);
            Assert.Equal(expected[i].Feedback, actual[i].Feedback);
            Assert.Equal(expected[i].OriginalPositionInGuess, actual[i].OriginalPositionInGuess);
        }
    }

    [Fact]
    public void UpdateCriteriaFromScoredGuess_Scenario_CRAME_vs_SLATE()
    {
        // Guess: CRANE, Answer: SLATE => c-, r-, aY, n-, eY
        string guess = "crane";
        ScoredLetter[] scoredGuess = new[]
        {
            new ScoredLetter('c', LetterFeedback.Grey, 0),
            new ScoredLetter('r', LetterFeedback.Grey, 1),
            new ScoredLetter('a', LetterFeedback.Yellow, 2),
            new ScoredLetter('n', LetterFeedback.Grey, 3),
            new ScoredLetter('e', LetterFeedback.Yellow, 4)
        };

        var criteriaToUpdate = new FilteringCriteria(initializeCollections: true); // Start with empty/base criteria

        CountReductionCalculator.UpdateCriteriaFromScoredGuess(guess, scoredGuess, criteriaToUpdate);

        // Assertions for criteriaToUpdate state:
        // MustBePresentChars: { 'a', 'e' }
        Assert.Contains('a', criteriaToUpdate.MustBePresentChars);
        Assert.Contains('e', criteriaToUpdate.MustBePresentChars);
        Assert.Equal(2, criteriaToUpdate.MustBePresentChars.Count);

        // GlobalExcluded: { 'c', 'r', 'n' } (a and e are yellow, so not globally excluded)
        Assert.Contains('c', criteriaToUpdate.GlobalExcluded);
        Assert.Contains('r', criteriaToUpdate.GlobalExcluded);
        Assert.Contains('n', criteriaToUpdate.GlobalExcluded);
        Assert.Equal(3, criteriaToUpdate.GlobalExcluded.Count);
        Assert.DoesNotContain('a', criteriaToUpdate.GlobalExcluded);
        Assert.DoesNotContain('e', criteriaToUpdate.GlobalExcluded);

        // PositionExcluded: pos[2] should exclude 'a', pos[4] should exclude 'e'
        Assert.Null(criteriaToUpdate.PositionExcluded[0]);
        Assert.Null(criteriaToUpdate.PositionExcluded[1]);
        Assert.Equal("a", criteriaToUpdate.PositionExcluded[2]);
        Assert.Null(criteriaToUpdate.PositionExcluded[3]);
        Assert.Equal("e", criteriaToUpdate.PositionExcluded[4]);

        // RegexArray: (chars in [^...] should be sorted alphabetically by SortedSet)
        // c,r,n are global. a is yellow at 2. e is yellow at 4.
        // regex[0] = "[^cnr]"
        // regex[1] = "[^cnr]"
        // regex[2] = "[^acnr]" (a excluded from pos 2, c,n,r globally)
        // regex[3] = "[^cnr]"
        // regex[4] = "[^cenr]" (e excluded from pos 4, c,n,r globally)
        Assert.Equal("[^cnr]", criteriaToUpdate.RegexArray[0]);
        Assert.Equal("[^cnr]", criteriaToUpdate.RegexArray[1]);
        Assert.Equal("[^acnr]", criteriaToUpdate.RegexArray[2]);
        Assert.Equal("[^cnr]", criteriaToUpdate.RegexArray[3]);
        Assert.Equal("[^cenr]", criteriaToUpdate.RegexArray[4]);
    }

    [Fact]
    public void UpdateCriteriaFromScoredGuess_Scenario_APPLE_vs_APPLY() 
    {
        // Guess: APPLE, Answer: APPLY => aG, pG, pG, lG, e-
        string guess = "apple";
        ScoredLetter[] scoredGuess = new[]
        {
            new ScoredLetter('a', LetterFeedback.Green, 0),
            new ScoredLetter('p', LetterFeedback.Green, 1),
            new ScoredLetter('p', LetterFeedback.Green, 2),
            new ScoredLetter('l', LetterFeedback.Green, 3),
            new ScoredLetter('e', LetterFeedback.Grey, 4)
        };

        var criteriaToUpdate = new FilteringCriteria(initializeCollections: true);
        CountReductionCalculator.UpdateCriteriaFromScoredGuess(guess, scoredGuess, criteriaToUpdate);

        // MustBePresentChars: { 'a', 'p', 'l' } (e is grey and not present in this list)
        Assert.Contains('a', criteriaToUpdate.MustBePresentChars);
        Assert.Contains('p', criteriaToUpdate.MustBePresentChars);
        Assert.Contains('l', criteriaToUpdate.MustBePresentChars);
        Assert.Equal(3, criteriaToUpdate.MustBePresentChars.Count);
        Assert.DoesNotContain('e', criteriaToUpdate.MustBePresentChars);

        // GlobalExcluded: { 'e' } (a,p,l are green, so not globally excluded)
        Assert.Contains('e', criteriaToUpdate.GlobalExcluded);
        Assert.Single(criteriaToUpdate.GlobalExcluded);

        // PositionExcluded: all null because greens override
        Assert.Null(criteriaToUpdate.PositionExcluded[0]);
        Assert.Null(criteriaToUpdate.PositionExcluded[1]);
        Assert.Null(criteriaToUpdate.PositionExcluded[2]);
        Assert.Null(criteriaToUpdate.PositionExcluded[3]);
        Assert.Null(criteriaToUpdate.PositionExcluded[4]);

        // RegexArray:
        // regex[0] = "a"
        // regex[1] = "p"
        // regex[2] = "p"
        // regex[3] = "l"
        // regex[4] = "[^e]" (e is globally excluded, no other exclusions for this spot)
        Assert.Equal("a", criteriaToUpdate.RegexArray[0]);
        Assert.Equal("p", criteriaToUpdate.RegexArray[1]);
        Assert.Equal("p", criteriaToUpdate.RegexArray[2]);
        Assert.Equal("l", criteriaToUpdate.RegexArray[3]);
        Assert.Equal("[^e]", criteriaToUpdate.RegexArray[4]);
    }


    [Fact]
    public async Task CalculateWordAsync_SimpleInitialState_ReturnsExpectedWords()
    {
        // Arrange
        // We instantiate Wordle, but immediately override its state to avoid file dependencies
        // and to control the test environment precisely.
        var wordle = new Wordle(); 

        // Define the current list of possible answers
        wordle.filtered = new[] { "apple", "apply", "artsy" }; // Changed apricot to artsy (5-letter)
            
        // Define the words to be considered as the next guess by the calculator
        // This list will be used as 'wordsToConsider' based on the logic in CalculateWordAsync
        wordle.sortedWords = new[] { "crane", "slate", "audio" }; 
            
        // Ensure 'wordsToConsider' becomes 'wordle.sortedWords':
        // To make initialWordsToConsider = wordle.sortedWords, we need 
        // (wordle.filtered.Length == 0 || wordle.filtered.Length > wordle.startWords.Count()) to be true.
        // wordle.filtered.Length is 3. Set startWords.Count() to be less than 3.
        wordle.startWords = new[] { "dummy1", "dummy2" }; // Ensures 3 > 2

        // Set initial game state (simulating a first guess, so no prior restrictions)
        wordle.regexArray = new string[5]; // All nulls
        wordle.globalExcluded = new List<char>(); // Empty
        wordle.positionExcluded = new string[5]; // All nulls
        wordle.possibles = new string[5]; // All nulls (means initialMustBePresentChars will be empty)
        wordle.turnCount = 1; // Part of Wordle state, though not directly used by CalculateWordAsync logic

        var calculator = new CountReductionCalculator(maxDop: 1); // Use maxDop:1 for deterministic test

        // Act
        var bestWordsEnumerable = await calculator.CalculateWordAsync(wordle);
        var bestWords = bestWordsEnumerable.ToList();

        // Assert
        // Manual calculation with wordle.filtered = { "apple", "apply", "artsy" }
        // Guess "crane":
        //   vs "apple":  C(-),R(-),A(Y),N(-),E(Y) -> Filtered: {"apple", "apply"} (artsy fails on missing 'e') -> Count = 2
        //   vs "apply":  C(-),R(-),A(Y),N(-),E(Y) -> Filtered: {"apple", "apply"} (artsy fails on missing 'e') -> Count = 2
        //   vs "artsy": C(-),R(Y),A(Y),N(-),E(-) -> Filtered: {"artsy"} (apple/apply fail on missing 'r') -> Count = 1
        //   Total remaining for "crane" = 2+2+1 = 5. Score = 5/3 = 1.66...
        //
        // Guess "slate":
        //   vs "apple":  S(-),L(Y),A(Y),T(-),E(G) -> Filtered: {"apple", "apply"} (artsy fails on missing 'l') -> Count = 2
        //   vs "apply":  S(-),L(Y),A(Y),T(-),E(G) -> Filtered: {"apple", "apply"} (artsy fails on missing 'l') -> Count = 2
        //   vs "artsy": S(Y),L(-),A(Y),T(Y),E(-) -> Filtered: {"artsy"} (apple/apply fail on missing 's' or 't') -> Count = 1
        //   Total remaining for "slate" = 2+2+1 = 5. Score = 5/3 = 1.66...
        //
        // Guess "audio":
        //   vs "apple":  A(G),U(-),D(-),I(-),O(-) -> Filtered: {"apple", "apply", "artsy"} (all match: starts 'a', no u,d,i,o) -> Count = 3
        //   vs "apply":  A(G),U(-),D(-),I(-),O(-) -> Filtered: {"apple", "apply", "artsy"} -> Count = 3
        //   vs "artsy": A(G),U(-),D(-),I(-),O(-) -> Filtered: {"apple", "apply", "artsy"} -> Count = 3
        //   Total remaining for "audio" = 3+3+3 = 9. Score = 9/3 = 3.0
        //
        // Min score is 1.66..., achieved by "crane" and "slate".
        // Expected: "crane", "slate" (ordered alphabetically)

        Assert.NotNull(bestWords);
        Assert.Equal(2, bestWords.Count);
        Assert.Contains("crane", bestWords);
        Assert.Contains("slate", bestWords);
        // Ensure order if necessary, or check as a set.
        // The method itself orders them: .OrderBy(x => x)
        Assert.Equal("crane", bestWords[0]);
        Assert.Equal("slate", bestWords[1]);
    }

    [Fact]
    public async Task CalculateWordAsync_Scenario_A1STER_GreysWithViolatingGuess()
    {
        // Simulates the state after a guess like "a1ster"
        // 'a' is Yellow (known, but not at pos 0)
        // 's', 't', 'e', 'r' are Grey (globally excluded)
        var wordle = new Wordle();
        var calculator = new CountReductionCalculator(maxDop: 1); // Deterministic for test

        // Setup Wordle state as if "a1ster" was processed by ProcessGuess
        wordle.globalExcluded = new List<char> { 's', 't', 'e', 'r' };
            
        wordle.possibles = new string[5];
        wordle.possibles[0] = "a"; // 'a' was yellow at pos 0
            
        wordle.positionExcluded = new string[5];
        wordle.positionExcluded[0] = "a"; // 'a' cannot be at pos 0 again if it was yellow there

        // Build regexArray similar to how ProcessGuess would
        wordle.regexArray = new string[5];
        var combinedExclusionsFirstPos = new string(new char[] { 'a' }.Concat(wordle.globalExcluded).Distinct().OrderBy(c => c).ToArray());
        wordle.regexArray[0] = "[^" + combinedExclusionsFirstPos + "]"; // e.g., "[^aerst]"
        for (int i = 1; i < 5; i++)
        {
            var combinedExclusionsOtherPos = new string(wordle.globalExcluded.Distinct().OrderBy(c => c).ToArray());
            wordle.regexArray[i] = "[^" + combinedExclusionsOtherPos + "]"; // e.g., "[^erst]"
        }
            
        wordle.filtered = new[] { "bacon", "canal", "madam", "valid", "panic" };
        // No need to pre-filter wordle.filtered with WordMatchesCriteriaManual for this test, 
        // as CalculateWordAsync no longer strictly pre-filters its inputs that way.

        // Words to consider for the next guess. 
        // "crane" (3 greys: r,a,n,e -> e,r) - should be filtered by post-filter if it scores best
        // "slate" (3 greys: s,l,a,t,e -> s,t,e) - should be filtered by post-filter
        // "audio" (0 greys, 'a' is yellow, 'o' is new)
        // "apply" (0 greys, 'a' is yellow, 'p','l','y' are new)
        // "bacon" (0 greys, 'a' is yellow)
        // "valid" (0 greys, 'a' is yellow)
        // "apple" (1 grey: 'e')
        var wordsToConsiderForNextGuess = new[] { "crane", "slate", "bacon", "audio", "valid", "apple", "apply" }; 
        wordle.sortedWords = wordsToConsiderForNextGuess;
        wordle.startWords = new[] { "dummy" }; // To ensure sortedWords is used
            
        // Act
        var bestWordsEnumerable = await calculator.CalculateWordAsync(wordle);
        var bestWords = bestWordsEnumerable.ToList();

        // Assertions:
        // With the new post-filter (maxAllowedKnownGreysThreshold = 3, meaning < 3 allowed):
        // "crane" has 2 known greys (e, r) from "a1ster". Kept if scores best.
        // "slate" has 3 known greys (s, t, e) from "a1ster". Filtered out by post-filter if scores best.
        // "apple" has 1 known grey ('e'). Kept.

        // Scores (approximate, focusing on which ones are valid candidates now):
        // wordsToConsiderLocal will be the full wordsToConsiderForNextGuess list.
        // "bacon": 1.0 (heuristic)
        // "valid": 1.0 (heuristic)
        // "audio": Score will be calculated. Contains 'a', no s,t,e,r. Valid candidate.
        // "apply": Score will be calculated. Contains 'a', no s,t,e,r. Valid candidate.
        // "apple": Score will be calculated. Contains 'a', has 'e' (1 grey). Valid candidate.
        // "crane": Score will be calculated. Contains 'a', has 'r','e' (2 greys). Valid candidate.
        // "slate": Score will be calculated. Contains 'a', has 's','t','e' (3 greys). Will be removed by post-filter if it has minScore.

        // Let's assume for this test that "slate" would have had the best score, 
        // but is filtered out by the post-filter.
        // And that "audio", "bacon", "valid" end up being the next best.
        // This test is now more about the post-filtering behavior.

        Assert.False(bestWords.Contains("slate"), "slate has 3 known greys and should be filtered out by the post-filter if it had a min score.");
            
        // We expect words with < 3 known greys if they score well.
        // The exact best words depend on scores, but they should not include words with >= 3 known greys if other options exist.
        // For this specific setup, let's assume 'audio', 'bacon', 'valid' are the best after 'slate' is removed.
        var expectedBestAfterPostFilter = new List<string> { "audio", "bacon", "valid" }; // Example
            
        // If the actual best words are different due to scoring, this part of the test might need adjustment
        // based on actual calculated scores. The key is that "slate" (or any word with >=3 greys) is not there
        // *if* it was a top scorer and got filtered, and other valid words took its place.
            
        // Check that all returned words have < 3 known grey letters
        foreach (var word in bestWords)
        {
            int greyCount = word.Count(c => wordle.globalExcluded.Contains(c));
            Assert.True(greyCount < 3, $"Word '{word}' has {greyCount} known grey letters, exceeding threshold.");
        }

        // If after post-filtering, the list is empty, the original best (even with many greys) would be returned.
        // This test case assumes the post-filter *does* remove words and others remain.
        // If the list was e.g. only {"slate"} with min score, then {"slate"} would be returned.
        // To make this test robust, we need to ensure there are other good candidates.
            
        // For now, let's verify the general principle: if slate was a candidate, it's gone.
        // And the remaining ones are valid in terms of grey count.
        // The exact content of bestWords depends on the scoring, which is complex to mock perfectly here.
        // So, the primary check is the absence of overly-grey words if better options exist.
    }

    [Fact]
    public async Task CalculateWordAsync_Integration_A1STER_Scenario_VerifyScoresAndBestWord()
    {
        // Scenario: Previous guess "a1ster" -> a (Yellow@0), s,t,e,r (Grey)
        var wordle = new Wordle();
        var initialSortedWords = new List<string> { "raise", "arise", "crane", "slate", "trace", "least", "audio" };
        wordle.startWords = new List<string>(initialSortedWords); 
        wordle.sortedWords = initialSortedWords.OrderBy(w => w).ToList(); 

        // Simulate Wordle state after "a1ster"
        wordle.globalExcluded = new List<char> { 's', 't', 'e', 'r' };
        wordle.possibles = new string[5];
        wordle.possibles[0] = "a"; 
        wordle.positionExcluded = new string[5];
        wordle.positionExcluded[0] = "a"; 

        wordle.regexArray = new string[5];
        var combinedExclusionsFirstPos = new string(new char[] { 'a' }.Concat(wordle.globalExcluded).Distinct().OrderBy(c => c).ToArray());
        wordle.regexArray[0] = "[^" + combinedExclusionsFirstPos + "]"; 
        for (int i = 1; i < 5; i++)
        {
            var combinedExclusionsOtherPos = new string(wordle.globalExcluded.Distinct().OrderBy(c => c).ToArray());
            wordle.regexArray[i] = "[^" + combinedExclusionsOtherPos + "]"; 
        }

        wordle.filtered = new[] { "bacon", "canal", "madam", "valid", "panic", "human", "final", "local", "focal", "vocal",
            "gamma", "kappa", "naval", "papal", "qualm", "zonal", "bayou", "cacao", "dandy", "fancy",
            "flack", "flank", "gawky", "guava", "handy", "happy", "iliac", "inlay", "jazzy", "khaki",
            "knack", "koala", "lanky", "laugh", "lilac", "llama", "loamy", "loyal", "macaw", "macho",
            "madly", "mafia", "magic", "magma", "mambo", "mamma", "mammy", "manga", "mango", "mangy",
            "mania", "manic", "manly", "maxim", "mocha", "modal", "nanny", "ninja", "nomad", "offal",
            "paddy", "pagan", "piano", "pizza", "plaid", "plain", "plank", "plaza", "polka", "pupal",
            "quack", "quail", "vapid", "villa", "viola", "vodka", "voila", "wacky", "wagon", "whack",
            "woman" }.OrderBy(w => w).ToArray(); 

        var calculator = new CountReductionCalculator(maxDop: 4); 

        Assert.Contains("audio", wordle.sortedWords); 
        string[] candidateGuesses = CountReductionCalculator.GetCandidateGuesses(wordle);
        Assert.Equal(wordle.sortedWords.Count(), candidateGuesses.Length);
        Assert.Contains("crane", candidateGuesses); 
        Assert.Contains("slate", candidateGuesses); 
        Assert.Contains("audio", candidateGuesses); 

        FilteringCriteria baseCriteria = CountReductionCalculator.CreateBaseFilteringCriteria(wordle);

        ConcurrentDictionary<string, double> wordScores = await CountReductionCalculator.ScoreCandidateGuessesAsync(
            candidateGuesses,
            wordle.filtered.ToArray(),
            baseCriteria,
            4, 
            new HashSet<string>(wordle.filtered) 
        );

        Assert.True(wordScores.Count > 0, "Word scores dictionary should not be empty.");

        var distinctScores = wordScores.Values.Distinct().ToList();
        Assert.True(distinctScores.Count > 1, "Expected multiple distinct scores for different words.");

        double audioScore = wordScores.GetValueOrDefault("audio", double.MaxValue);
        double craneScore = wordScores.GetValueOrDefault("crane", double.MaxValue);
        double slateScore = wordScores.GetValueOrDefault("slate", double.MaxValue);

        Assert.True(audioScore < craneScore, $"Expected 'audio' (0 greys) to score better than 'crane' (2 greys). audio: {audioScore}, crane: {craneScore}");
        Assert.True(craneScore < slateScore, $"Expected 'crane' (2 greys) to score better than 'slate' (3 greys). crane: {craneScore}, slate: {slateScore}");

        var bestWordsEnumerable = CountReductionCalculator.SelectBestWordsByScore(wordScores, wordle.filtered.ToArray());
        var bestWords = bestWordsEnumerable.ToList();

        Assert.True(bestWords.Any(), "Best words list should not be empty.");
        Assert.Contains("audio", bestWords); 
        Assert.DoesNotContain("slate", bestWords); 

        var finalBestWordsEnumerable = await calculator.CalculateWordAsync(wordle);
        var finalBestWords = finalBestWordsEnumerable.ToList();
        Assert.Equal(bestWords, finalBestWords); 
    }

    [Fact(Skip = "More of an integration test - quite slow")]
    public async Task CalculateWordAsync_Integration_A1STER_FullWordList_Temporary()
    {
        // Scenario: Previous guess "a1ster" -> a (Yellow@0), s,t,e,r (Grey)
        var wordle = new Wordle(); // This should load words via constructor
        // Ensure word lists are loaded
        Assert.True(wordle.sortedWords.Any(), "SortedWords should be loaded by Wordle constructor.");
        Assert.True(wordle.startWords.Any(), "StartWords should be loaded by Wordle constructor.");

        // Simulate Wordle state after "a1ster"
        wordle.globalExcluded = new List<char> { 's', 't', 'e', 'r' };
        wordle.possibles = new string[5];
        wordle.possibles[0] = "a"; // 'a' was yellow at pos 0
        wordle.positionExcluded = new string[5];
        wordle.positionExcluded[0] = "a"; // 'a' cannot be at pos 0

        wordle.regexArray = new string[5];
        var combinedExclusionsFirstPos = new string(new char[] { 'a' }.Concat(wordle.globalExcluded).Distinct().OrderBy(c => c).ToArray());
        wordle.regexArray[0] = "[^" + combinedExclusionsFirstPos + "]"; // "[^aerst]"
        for (int i = 1; i < 5; i++)
        {
            var combinedExclusionsOtherPos = new string(wordle.globalExcluded.Distinct().OrderBy(c => c).ToArray());
            wordle.regexArray[i] = "[^" + combinedExclusionsOtherPos + "]"; // "[^erst]"
        }

        // Filter the full word list based on the criteria from "a1ster"
        var initialCriteria = CountReductionCalculator.CreateBaseFilteringCriteria(wordle);
        // Use wordle.sortedWords as the source of all potential words if allWords isn't public
        wordle.filtered = wordle.sortedWords.Where(w => CountReductionCalculator.WordMatchesCriteriaManual(w, initialCriteria)).ToArray();
        Assert.True(wordle.filtered.Any(), "Filtered list should not be empty after applying A1STER criteria.");
            
        int testMaxDop = 4; // Define maxDop for this test
        var calculator = new CountReductionCalculator(maxDop: testMaxDop); 

        // Step 1: Get Candidate Guesses
        string[] candidateGuesses = CountReductionCalculator.GetCandidateGuesses(wordle);
        Assert.True(candidateGuesses.Any(), "Candidate guesses should not be empty.");

        // Step 2: Create Base Filtering Criteria
        FilteringCriteria baseCriteria = CountReductionCalculator.CreateBaseFilteringCriteria(wordle);

        // Step 3: Score Candidate Guesses
        ConcurrentDictionary<string, double> wordScores = await CountReductionCalculator.ScoreCandidateGuessesAsync(
            candidateGuesses,
            wordle.filtered.ToArray(), // These are the possible answers
            baseCriteria,
            testMaxDop, // Use the defined maxDop for this test
            new HashSet<string>(wordle.filtered) // currentWordleFilteredForHeuristic
        );

        Assert.True(wordScores.Count > 0, "Word scores dictionary should not be empty.");

        var distinctScores = wordScores.Values.Distinct().ToList();
        // It's possible with a large list and specific scenario, only one score is produced if all other words are filtered out early
        Assert.True(distinctScores.Count > 100, "Expected multiple distinct scores for different words."); 

        // Step 4: Select Best Words By Score
        var bestWordsEnumerable = CountReductionCalculator.SelectBestWordsByScore(wordScores, wordle.filtered.ToArray());
        var bestWords = bestWordsEnumerable.ToList();

        Assert.True(bestWords.Any(), "Best words list should not be empty.");

        Assert.True(bestWords.Count < wordScores.Count, "Best words should be fewer than total scored words.");
            
        // Verify the full CalculateWordAsync method call
        var finalBestWordsEnumerable = await calculator.CalculateWordAsync(wordle);
        var finalBestWords = finalBestWordsEnumerable.ToList();
        Assert.Equal(bestWords.OrderBy(w => w), finalBestWords.OrderBy(w => w)); // Order for stable comparison
            

        // Output the best words for manual verification
        // Using Xunit.Abstractions.ITestOutputHelper would be better for test output, but Console.WriteLine is fine for a temp, skipped test.
        Console.WriteLine($"Temporary Test - {System.Reflection.MethodBase.GetCurrentMethod()?.Name} - Best words with full list for A1STER scenario:");
        foreach (var word in finalBestWords.Take(20)) // Print top 20
        {
            Console.WriteLine($"- {word} (Score: {wordScores.GetValueOrDefault(word, double.NaN)})");
        }
        Assert.True(finalBestWords.Any(), "Final best words should not be empty for manual check.");
    }

    [Fact]
    public async Task CalculateWordAsync_Scenario_A1STER_GreysWithViolatingGuess_Simplified()
    {
        var wordle = new Wordle();
        var calculator = new CountReductionCalculator(maxDop: 1);

        wordle.globalExcluded = new List<char> { 's', 't', 'e', 'r' };
        wordle.possibles = new string[5];
        wordle.possibles[0] = "a"; 
        wordle.positionExcluded = new string[5];
        wordle.positionExcluded[0] = "a";
        wordle.regexArray = new string[5];
        var combinedExclusionsFirstPos = new string(new char[] { 'a' }.Concat(wordle.globalExcluded).Distinct().OrderBy(c => c).ToArray());
        wordle.regexArray[0] = "[^" + combinedExclusionsFirstPos + "]";
        for (int i = 1; i < 5; i++)
        {
            var combinedExclusionsOtherPos = new string(wordle.globalExcluded.Distinct().OrderBy(c => c).ToArray());
            wordle.regexArray[i] = "[^" + combinedExclusionsOtherPos + "]";
        }
        wordle.filtered = new[] { "bacon", "canal", "madam", "valid", "panic" };
        var tempSortedWords = new List<string> { "crane", "slate", "bacon", "audio", "valid", "apple", "apply" };
        wordle.sortedWords = tempSortedWords.OrderBy(w=>w).ToList(); 
        wordle.startWords = new[] { "dummy" }; // To ensure sortedWords is used
            
        var bestWordsEnumerable = await calculator.CalculateWordAsync(wordle);
        var bestWords = bestWordsEnumerable.ToList();

        // Assert that no words with 3 or more known grey letters are returned *if* better options exist.
        // This is a general assertion; the integration test above handles specific score comparisons.
        bool anyWordHasTooManyGreys = false;
        if (bestWords.Any() && bestWords.Count < wordle.sortedWords.Count()) // Only if filtering happened
        {
            foreach (var word in bestWords)
            {
                int greyCount = word.Count(c => wordle.globalExcluded.Contains(c));
                if (greyCount >= 3)
                {
                    anyWordHasTooManyGreys = true;
                    break;
                }
            }
        }
        Assert.False(anyWordHasTooManyGreys, "No word with 3 or more known greys should be returned if better options exist.");
        // This assertion is tricky because if ALL good words have many greys, one might be returned.
        // The core idea is that the scoring should naturally deprioritize them.
        // The integration test is better for verifying this nuance.
        // For this simplified test, we'll just ensure the output is not empty if inputs are not.
        if (wordle.filtered.Any() && wordle.sortedWords.Any())
        {
            Assert.NotEmpty(bestWords);
        }
    }
}
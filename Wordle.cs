﻿using System.Text.RegularExpressions;
using WordleSharp.Calculators;
using WordleSharp.ProgressReporting;

namespace WordleSharp;

/// <summary>
/// Main game engine
/// </summary>
public class Wordle
{
    internal string[] regexArray;
    internal List<char> globalExcluded;
    internal string[] positionExcluded;
    internal string[] possibles;
    internal string[] filtered;
    internal IEnumerable<string> sortedWords;
    internal IEnumerable<string> startWords;
    internal int turnCount;
    public bool DisplayCountOnly;

    /// <summary>
    /// Gets the start words collection used for analysis.
    /// </summary>
    public IEnumerable<string> StartWords => startWords;

    private readonly int threshold;
    private INextWordCalculator calculator;
    private string knownAnswer;

    public Wordle(int threshold = 500)
    {
        this.threshold = threshold;
        sortedWords = LoadWordList();
        startWords = LoadStartWords();
        Reset();
    }

    public void Reset()
    {
        regexArray = new string[5];
        globalExcluded = new List<char>(21);
        positionExcluded = new string[5];
        possibles = new string[5];
        filtered = sortedWords.ToArray();
        turnCount = 1;
    }

    public WordleResult AutoPlay(string startWord, string answer)
    {
        Reset();
        string next = startWord;
        var attempts = new List<string>(8);
        while (filtered.Length > 1 && next != answer)
        {
            attempts.Add(next);
            string scoredWord = ScoreWord(next, answer);
            ProcessGuess(scoredWord);
            // Play pessimistically - this sort order puts the answer last in the list, ensuring it's only selected
            // if it's the only word remaining.
            // As this is hash code deterministic, the path to the solution might not always be the same with each
            // run
            next = GetBestNextWord(calculator)
                .OrderByDescending(x => Math.Abs(x.GetHashCode() - answer.GetHashCode()))
                .First();
            turnCount++;
        }

        return new WordleResult(answer, turnCount, attempts.ToArray());
    }

    public IEnumerable<WordleResult> GetBestStartWord(string answer)
    {
        return GetBestStartWord(answer, startWords);
    }

    public IEnumerable<WordleResult> GetBestStartWord(string answer, IEnumerable<string> startWords)
    {
        var results = new List<WordleResult>(startWords.Count() - 1);
        results.AddRange(startWords
            .Where(x => x != answer)
            .Select(word => AutoPlay(word, answer)));
        int minCount = results.Min(x => x.Turns);
        return results.Where(x => x.Turns == minCount);
    }

    public async Task<WordleResult> Analyse()
    {
        Reset();
        var entry = "";
        Console.WriteLine("Wordle analysis");
        var attempts = new List<string>();

        Console.WriteLine("Enter word. Letter followed by \'1\' means \'Correct letter, wrong location\'");
        Console.WriteLine("Followed by \'2\' means \'Right Letter, right Location\'");
        do
        {
            Console.Write("Word: ");
            entry = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            // If entry is a word followed by a question mark, respond with whether the word is valid according to
            // the remaining word list
            if (entry.LastIndexOf('?') == entry.Length - 1)
            {
                Console.WriteLine(filtered.Any(x => x == entry[..5]));
                continue;
            }

            entry = ProcessGuess(entry);

            int count = filtered.Length;
            if (count == 1)
            {
                if (DisplayCountOnly)
                {
                    Console.WriteLine("Single solution remaining");
                    return new WordleResult("[unknown]", turnCount, attempts.ToArray());
                }

                Console.WriteLine($"Solved! Answer is: {filtered.First()}");
                if (entry.IndexOfAny("01".ToCharArray()) >= 0)
                {
                    attempts.Add(CleanEntry(entry));
                    turnCount++;
                }

                var wordleResult = new WordleResult(
                    filtered.First(), turnCount, attempts.ToArray());
                return wordleResult;
            }

            attempts.Add(CleanEntry(entry));

            Console.WriteLine($"List narrowed down to {count} words");
            // As long as your start word isn\'t something daft like "lolly", this should be adequate.
            if (count < threshold && !DisplayCountOnly)
            {
                Console.WriteLine(string.Join(", ", filtered));

                // Use the spinner here
                var bestScoringWords = await RunTaskWithSpinner(
                    () => GetBestNextWordAsync(calculator),
                    "Calculating best next word..."
                );
                Console.WriteLine($"Best word(s) to try next: {string.Join(",", bestScoringWords)}");
            }

            turnCount++;
        } while (!string.IsNullOrWhiteSpace(entry));

        return new WordleResult("[unknown]", turnCount, attempts.ToArray());
    }

    private static async Task<T> RunTaskWithSpinner<T>(Func<Task<T>> action, string message)
    {
        var spinnerChars = new[] { '|', '/', '-', '\\' };
        var spinnerIndex = 0;
        var cts = new CancellationTokenSource();

        Console.Write(message + " ");

        var spinnerTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                Console.Write(spinnerChars[spinnerIndex]);
                spinnerIndex = (spinnerIndex + 1) % spinnerChars.Length;
                await Task.Delay(150, cts.Token).ContinueWith(_ => { });
                if (!cts.Token.IsCancellationRequested)
                {
                    Console.Write('\r'); // Return to the beginning of the line
                    Console.Write(message + " "); // Redraw message in case it was overwritten
                }
            }
        }, cts.Token);

        T result;
        try
        {
            result = await action();
        }
        finally
        {
            cts.Cancel();
            try
            {
                await spinnerTask; // Wait for the spinner task to acknowledge cancellation
            }
            catch (TaskCanceledException)
            {
                /* Expected */
            }

            Console.Write("\r" + new string(' ', message.Length + 2) + "\r"); // Clear the spinner line
        }

        return result;
    }

    private string ProcessGuess(string guess)
    {
        // Two words separated by a comma mean "score first, assuming second word is answer"
        if (!string.IsNullOrEmpty(knownAnswer) || Regex.IsMatch(guess, "[a-z]{5},[a-z]{5}"))
        {
            string[] split = guess.Split(',');
            guess = split[0];
            knownAnswer ??= split[1];
            guess = ScoreWord(guess, knownAnswer);
        }

        // Word followed by exclamation mark means "correct answer"
        if (Regex.IsMatch(guess, "[a-z]{5}!"))
        {
            var correctGuess = new Regex("([a-z])");
            guess = correctGuess.Replace(guess, @"${1}2").Substring(0, 10);
        }

        // Add suffix of 0 to any letters that don't have any specifier
        var zeroReplace = new Regex(@"([a-z])(?![012])");
        guess = zeroReplace.Replace(guess, @"${1}0");
        var loopExclude = new char[5];
        var loopPossible = new char[5];
        for (var i = 0; i < 5; i++)
        {
            string substring = guess.Substring(2 * i, 2);

            switch (int.Parse(substring[1].ToString()))
            {
                case 0:
                {
                    // Nicety - if you've already said that the letter is a 2, no need to say again
                    // BUT only if it's the same letter - in non-hard mode, different letters can be tried
                    if (Regex.IsMatch(regexArray[i] ?? string.Empty, "^[a-z]$") &&
                        regexArray[i] == substring[0].ToString())
                    {
                        char[] guessArray = guess.ToCharArray();
                        guessArray[2 * i + 1] = '2';
                        guess = new string(guessArray);
                        continue;
                    }

                    loopExclude[i] = substring[0];

                    break;
                }
                case 1:
                {
                    loopPossible[i] = substring[0];
                    break;
                }
                case 2:
                {
                    regexArray[i] = substring[0].ToString();
                    break;
                }
                default:
                {
                    throw new InvalidOperationException($"Invalid substring: {substring[1]}");
                }
            }
        }

        // This approach overcomes the double letter in guess, but not in answer scenario.
        for (var i = 0; i < 5; i++)
        {
            positionExcluded[i] += loopExclude[i];
            possibles[i] += loopPossible[i];
        }

        var globalToAdd = loopExclude.Except(loopPossible);
        globalExcluded.AddRange(globalToAdd);

        for (var i = 0; i < 5; i++)
        {
            if (Regex.IsMatch(regexArray[i] ?? string.Empty, "^[a-z]$"))
            {
                continue;
            }

            var excluded = (
                    positionExcluded[i] +
                    possibles[i] +
                    new string(globalExcluded.ToArray()))
                .ToCharArray()
                .Distinct()
                .Where(c => Regex.IsMatch(c.ToString(), "[a-z]"));
            regexArray[i] = $"[^{string.Join("", excluded)}]";
        }

        filtered = filtered
            .Where(word => Regex.IsMatch(word, string.Join("", regexArray)))
            .ToArray();

        foreach (char letter in
                 string.Join("", possibles)
                     .ToCharArray()
                     .Where(c => Regex.IsMatch(c.ToString(), "[a-z]"))
                     .Distinct())
        {
            filtered = filtered.Where(f => f.Contains(letter)).ToArray();
        }

        return guess;
    }

    private IEnumerable<string> GetBestNextWord(INextWordCalculator calc)
    {
        return calc.CalculateWord(this);
    }

    private Task<IEnumerable<string>> GetBestNextWordAsync(INextWordCalculator calc, IProgressUpdater progressUpdater)
    {
        return calc.CalculateWordAsync(this, progressUpdater);
    }

    private Task<IEnumerable<string>> GetBestNextWordAsync(INextWordCalculator calc)
    {
        return calc.CalculateWordAsync(this);
    }

    public static string ScoreWord(string guess, string target)
    {
        char[] targetArray = target.ToCharArray();
        char[] guessArray = guess.ToCharArray();
        var result = new int[5];
        var sb = new System.Text.StringBuilder(10);
        for (var i = 0; i < 5; i++)
        {
            // First pass, identify all green
            if (guessArray[i] == targetArray[i])
            {
                result[i] = 2;
                targetArray[i] = '?';
            }
        }

        for (var i = 0; i < 5; i++)
        {
            // Second pass, skip anything already green...
            if (result[i] == 2)
            {
                continue;
            }

            // then, if the answer contains the letter, mark it yellow and prevent it from being counted again.
            // This overcomes the issue if the guess contains repeated letters, but the answer doesn't,
            // only the first is marked yellow
            if (targetArray.Any(x => x == guessArray[i]))
            {
                result[i] = 1;
                var pattern = new Regex(guessArray[i].ToString());
                targetArray = pattern
                    .Replace(string.Join("", targetArray), "?", 1)
                    .ToCharArray();
            }
            else
            {
                result[i] = 0;
            }
        }

        for (var i = 0; i < 5; i++)
        {
            sb.Append(guess[i]).Append(result[i]);
        }

        return sb.ToString();
    }

    private static string CleanEntry(string entry)
    {
        var rx = new Regex("[^a-z]");
        return rx.Replace(entry, "");
    }

    private static IEnumerable<string> LoadWordList()
    {
        string? currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (currentPath == null)
            throw new InvalidOperationException("Unable to determine application directory");

        string[] text = File.ReadAllLines(Path.Combine(currentPath, "WordLists", "Answers.txt"));
        return text;
    }

    private static IEnumerable<string> LoadStartWords()
    {
        string? currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (currentPath == null)
            throw new InvalidOperationException("Unable to determine application directory");

        string[] text = File.ReadAllLines(Path.Combine(currentPath, "WordLists", "StartWords.txt"));
        return text;
    }

    public void SetNextWordCalculator(INextWordCalculator calc)
    {
        calculator = calc;
    }

    public string[] GetWordsContainingLetter(string letter)
    {
        var dic = new Dictionary<string, int>();
        foreach (string word in sortedWords)
        {
            int sum = 0;
            foreach (char c in word.ToCharArray().Distinct())
            {
                if (letter.Contains(c))
                {
                    sum++;
                }
            }

            dic.Add(word, sum);
        }

        int max = dic.Values.Max();
        return dic
            .Where(kvp => kvp.Value == max)
            .Select(kvp => kvp.Key)
            .ToArray();
    }

    public async Task<WordleResult> Analyse(IProgressUpdater? progressUpdater = null)
    {
        Reset();
        var entry = "";
        Console.WriteLine("Wordle analysis");
        var attempts = new List<string>();

        Console.WriteLine("Enter word. Letter followed by \'1\' means \'Correct letter, wrong location\'");
        Console.WriteLine("Followed by \'2\' means \'Right Letter, right Location\'");
        do
        {
            Console.Write("Word: ");
            entry = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            // If entry is a word followed by a question mark, respond with whether the word is valid according to
            // the remaining word list
            if (entry.LastIndexOf('?') == entry.Length - 1)
            {
                Console.WriteLine(filtered.Any(x => x == entry[..5]));
                continue;
            }

            entry = ProcessGuess(entry);

            int count = filtered.Length;
            if (count == 1)
            {
                if (DisplayCountOnly)
                {
                    Console.WriteLine("Single solution remaining");
                    return new WordleResult("[unknown]", turnCount, attempts.ToArray());
                }

                Console.WriteLine($"Solved! Answer is: {filtered.First()}");
                if (entry.IndexOfAny("01".ToCharArray()) >= 0)
                {
                    attempts.Add(CleanEntry(entry));
                    turnCount++;
                }

                var wordleResult = new WordleResult(
                    filtered.First(), turnCount, attempts.ToArray());
                return wordleResult;
            }

            attempts.Add(CleanEntry(entry));

            Console.WriteLine($"List narrowed down to {count} words");
            // As long as your start word isn\'t something daft like "lolly", this should be adequate.
            if (count < threshold && !DisplayCountOnly)
            {
                Console.WriteLine(string.Join(", ", filtered));

                // Use the spinner here, or progress updater if provided
                var bestScoringWords = progressUpdater != null
                    ? await GetBestNextWordAsync(calculator, progressUpdater)
                    : await RunTaskWithSpinner(
                        () => GetBestNextWordAsync(calculator),
                        "Calculating best next word..."
                    );
                Console.WriteLine($"Best word(s) to try next: {string.Join(",", bestScoringWords)}");
            }

            turnCount++;
        } while (!string.IsNullOrWhiteSpace(entry));

        return new WordleResult("[unknown]", turnCount, attempts.ToArray());
    }
}
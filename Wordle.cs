using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;
using WordleSharp.Calculators;

namespace WordleSharp
{
    public class Wordle
    {
        private string[] regexArray;
        private List<char> globalExcluded;
        private string[] positionExcluded;
        private string[] possibles;
        private string[] filtered;
        private readonly IEnumerable<string> sortedWords;
        private readonly IEnumerable<string> startWords;
        private int turnCount;
        public bool DisplayCountOnly;
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
                // Play pessimistically - this sort order puts the answer last in the list, ensuring it's ony selected
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

        public WordleResult Analyse()
        {
            Reset();
            var entry = "";
            Console.WriteLine("Wordle analysis");
            var attempts = new List<string>();

            Console.WriteLine("Enter word. Letter followed by '1' means 'Correct letter, wrong location'");
            Console.WriteLine("Followed by '2' means 'Right Letter, right Location'");
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
                if (entry.LastIndexOf('?') == entry.Length -1)
                {
                    Console.WriteLine(sortedWords.Any(x => x == entry.Substring(0, 5)));
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
                    Console.WriteLine($"Solved! Answer is: ({filtered.First()})");
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
                // As long as your start word isn't something daft like "lolly", this should be adequate.
                if (count < threshold && !DisplayCountOnly)
                {
                    Console.WriteLine(string.Join(", ", filtered));
                    var bestScoringWords = GetBestNextWord(calculator);
                    Console.WriteLine($"Best word(s) to try next: {string.Join(",", bestScoringWords)}");
                }
                turnCount++;
            } while (!string.IsNullOrWhiteSpace(entry));

            return new WordleResult("[unknown]", turnCount, attempts.ToArray());
        }

        private string ProcessGuess(string guess)
        {
            // Two words separated by a comma mean "score first, assuming second word is answer
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
                        if (Regex.IsMatch(regexArray[i] ?? string.Empty, "^[a-z]$"))
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
                if (Regex.IsMatch(regexArray[i] ?? string.Empty, "^[a-z]$")) { continue; }

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
                         .Where(c => Regex.IsMatch(c.ToString(), "[a-z]") )
                         .Distinct() )
            {
                filtered = filtered.Where(f => Regex.IsMatch(f, letter.ToString())).ToArray();
            }
            return guess;
        }

        private IEnumerable<string> GetBestNextWord(INextWordCalculator calc)
        {
            return calc.CalculateWord(filtered);
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
                if (result[i] == 2) { continue; }

                // then, if the answer contains the letter, mark it yellow and prevent it from being counted again.
                // This overcomes the issue if the guess contains repeated letters, but the answer doesn't,
                // only the first is marked yellow
                if (targetArray.Any(x => x == guessArray[i]))
                {
                    result[i] = 1;
                    var pattern = new Regex(guessArray[i].ToString());
                    targetArray = pattern
                        .Replace(string.Join("",targetArray), "?", 1)
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
            string[] text = System.IO.File.ReadAllLines(".\\WordLists\\Answers.txt");
            return text;
        }

        private static IEnumerable<string> LoadStartWords()
        {
            string[] text = System.IO.File.ReadAllLines(".\\WordLists\\StartWords.txt");
            return text;
        }

        public void SetNextWordCalculator(INextWordCalculator calc)
        {
            calculator = calc;
        }
    }
}




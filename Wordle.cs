using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace WordleSharp
{
    public class AttemptedWords
    {
        private IEnumerable<string> Words { get; }

        public AttemptedWords(IEnumerable<string> words)
        {
            Words = words;
        }

    }

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

    public interface INextWordCalculator
    {
        public IEnumerable<string> CalculateWord(IEnumerable<string> wordList);
    }

    public class Wordle
    {
        string[] _regexArray;
        List<char> _globalExcluded;
        string[] _positionExcluded;
        string[] _possibles;
        string[] _filtered;
        IEnumerable<string> _sortedWords;
        IEnumerable<string> _startWords;
        int _turnCount;
        bool _displayCountOnly;
        int _threshold;
        INextWordCalculator _calculator;
        string _answer;


        public Wordle(int threshold = 500)
        {
            _threshold = threshold;
            _sortedWords = LoadWordList();
            Debug.Assert(_sortedWords.Count() > 1000);
            _startWords = LoadStartWords();
            Reset();
        }

        public void Reset()
        {
            _regexArray = new string[5];
            _globalExcluded = new List<char>(21);
            _positionExcluded = new string[5];
            _possibles = new string[5];
            _filtered = _sortedWords.ToArray();
        }

        public WordleResult AutoPlay(string startWord, string answer)
        {
            Reset();
            string next = startWord;
            var attempts = new List<string>(8);
            while (_filtered.Length > 1 && next != answer)
            {
                attempts.Add(next);
                string scoredWord = ScoreWord(next, answer);
                ProcessGuess(scoredWord);
                // Play pessimistically - this sort order puts the answer last in the list, ensuring it's ony selected if it's the only word remaining.
                // As this is hash code deterministic, the path to the solution might not always be the same with each run
                next = GetBestNextWord(_calculator)
                    .OrderByDescending(x => Math.Abs(x.GetHashCode() - answer.GetHashCode()))
                    .First();
                _turnCount++;
            }
            return new WordleResult(answer, _turnCount, attempts.ToArray());
        }

        public WordleResult[] GetBestStartWord(string answer)
        {
            return GetBestStartWord(answer, _startWords);
        }

        public WordleResult[] GetBestStartWord(string answer, IEnumerable<string> startWords)
        {
            throw new NotImplementedException();
        }

        public WordleResult Analyse()
        {
            Reset();
            var entry = "";
            Console.WriteLine("Wordle analysis");
            var attempts = new List<string>();

            Console.WriteLine("Enter word. Letter followed by '1' means 'Correct letter, wrong location'. Followed by '2' means 'Right Letter, right Location'");
            do
            {
                Console.Write("Word: ");
                entry = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }
                // If entry is a word followed by a question mark, respond with whether the word is valid according to the remaining word list
                if (entry.LastIndexOf('?') == entry.Length -1)
                {
                    Console.WriteLine(_sortedWords.Any(x => x == entry.Substring(0, 5)));
                    continue;
                }
                entry = ProcessGuess(entry);


                var count = _filtered.Length;
                if (count == 1)
                {
                    if (_displayCountOnly)
                    {
                        Console.WriteLine("Single solution remaining");
                        return new WordleResult("[unknown]", _turnCount, attempts.ToArray());
                    }
                    Console.WriteLine($"Solved! Answer is: ({_filtered.First()})");
                    if (entry.IndexOfAny("01".ToCharArray()) >= 0)
                    {
                        attempts.Add(CleanEntry(entry));
                        _turnCount++;
                    }
                    var wordleResult = new WordleResult(_filtered.First(), _turnCount, attempts.ToArray());
                    return wordleResult;
                }
                attempts.Add(CleanEntry(entry));

                Console.WriteLine($"List narrowed down to {count} words");
                // As long as your start word isn't something daft like "lolly", this should be adequate.
                if (count < _threshold && !_displayCountOnly)
                {
                    Console.WriteLine(string.Join(", ", _filtered));
                    var bestScoringWords = GetBestNextWord(_calculator);
                    Console.WriteLine($"Best word(s) to try next: {string.Join(",", bestScoringWords)}");
                }
                _turnCount++;
            } while (!string.IsNullOrWhiteSpace(entry));


            return new WordleResult("[unknown]", _turnCount, attempts.ToArray());
            
        }

        public string ProcessGuess(string guess)
        {
            // Two words separated by a comma mean "score first, assuming second word is answer
            if (!string.IsNullOrEmpty(_answer) || Regex.IsMatch(guess, "[a-z]{5},[a-z]{5}"))
            {
                var split = guess.Split(',');
                guess = split[0];
                _answer ??= split[1];
                guess = ScoreWord(guess, _answer);
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
                var substring = guess.Substring(2 * i, 2);

                switch (int.Parse(substring[1].ToString()))
                {
                    case 0:
                        {
                            // Nicety - if you've already said that the letter is a 2, no need to say again
                            if (Regex.IsMatch(_regexArray[i] ?? string.Empty, "^[a-z]$"))
                            {
                                var guessArray = guess.ToCharArray();
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
                            _regexArray[i] = substring[0].ToString();
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
                _positionExcluded[i] += loopExclude[i];
                _possibles[i] += loopPossible[i];
            }
            var globalToAdd = Enumerable.Except(loopExclude, loopPossible);
            _globalExcluded.AddRange(globalToAdd);

            for (var i = 0; i < 5; i++)
            {
                if (Regex.IsMatch(_regexArray[i] ?? String.Empty, "^[a-z]$")) { continue; }

                var excluded = (_positionExcluded[i] + _possibles[i] + new string(_globalExcluded.ToArray()))
                    .ToCharArray()
                    .Distinct()
                    .Where(c => Regex.IsMatch(c.ToString(), "[a-z]"));
                _regexArray[i] = $"[^{string.Join("", excluded)}]";
            }

            _filtered = _filtered
                .Where(word => Regex.IsMatch(word, string.Join("", _regexArray)))
                .ToArray();

            foreach (var letter in string.Join("", _possibles).ToCharArray().Where(c => Regex.IsMatch(c.ToString(), "[a-z]") ).Distinct() )
            {
                _filtered = _filtered.Where(f => Regex.IsMatch(f, letter.ToString())).ToArray();
            }
            return guess;
        }
        

        public IEnumerable<string> GetBestNextWord(INextWordCalculator calculator)
        {
            return calculator.CalculateWord(_filtered);
        }

        public static string ScoreWord(string guess, string target)
        {
            var targetArray = target.ToCharArray();
            var guessArray = guess.ToCharArray();
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
                // This overcomes the issue if the guess contains repeated letters, but the answer doesn't, only the first is marked yellow
                if (targetArray.Any(x => x == guessArray[i]))
                {
                    result[i] = 1;
                    Regex pattern = new Regex(guessArray[i].ToString());
                    targetArray = pattern.Replace(string.Join("",targetArray), "?", 1).ToCharArray();
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

        public static string CleanEntry(string entry)
        {
            var rx = new Regex("[^a-z]");
            return rx.Replace(entry, "");
        }

        public static IEnumerable<string> LoadWordList()
        {
            var text = System.IO.File.ReadAllLines(".\\WordLists\\Answers.txt");
            return text;
        }

        public static IEnumerable<string> LoadStartWords()
        {
            var text = System.IO.File.ReadAllLines(".\\WordLists\\StartWords.txt");
            return text;
        }

        public void SetNextWordCalculator(INextWordCalculator calc)
        {
            _calculator = calc;
        }
    }
}

namespace WordleSharp.Calculators
{
    public class LetterFrequencyCalculator : INextWordCalculator
    {
        public IEnumerable<string> CalculateWord(IEnumerable<string> wordList)
        {
            if (!wordList.Any())
            {
                Console.WriteLine("Error! Word list contained no entries!");
                return Enumerable.Empty<string>();
            }
            var dict = string.Join("", wordList)
                .ToCharArray()
                .GroupBy(x => x)
                .ToDictionary(k => k.Key, v => v.Count());
            var scores = new Dictionary<string, int>();
            foreach (string word in wordList)
            {
                int sum = 0;
                foreach (char letter in word.ToCharArray().Distinct())
                {
                    sum += dict[letter];
                }
                scores[word] = sum;
            }

            var lowestScore = scores.Max(x => x.Value);
            return scores.Where(x => x.Value == lowestScore).Select(s => s.Key);
        }
    }
}




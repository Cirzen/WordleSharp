using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WordleSharp.Calculators
{
    internal class CountReductionCalculator : INextWordCalculator
    {
        private static readonly HashSet<char> AToZ = new () { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
        private readonly int _maxDop;

        public CountReductionCalculator(int maxDop = 4)
        {
            _maxDop = maxDop;
        }
        
        public IEnumerable<string> CalculateWord(Wordle wordle)
        {
            var sw = Stopwatch.StartNew();
            var currentWordList = wordle.filtered;
            var results = new ConcurrentDictionary<string, List<int>>();
            //object locker = new();
            //int concurrencyCounter = 0;
            //int maxConcurrency = 0;
            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = _maxDop
            };

            Parallel.ForEach(currentWordList.ToArray(), options, (string guess) =>
            {
                //int concurrency = Interlocked.Increment(ref concurrencyCounter);
                //lock (locker) maxConcurrency = Math.Max(maxConcurrency, concurrency);
                
                results[guess] = new List<int>();
                foreach (string potentialAnswer in currentWordList)
                {
                    //if (guess == potentialAnswer) continue;
                    var scoredWord = Wordle.ScoreWord(guess, potentialAnswer);
                    results[guess].Add(
                        ProcessHypothetical(
                            scoredWord,
                            wordle.filtered.ToArray(),
                            wordle.positionExcluded.ToArray(),
                            wordle.possibles.ToArray(),
                            wordle.globalExcluded.ToList())
                        );
                }
                //Interlocked.Decrement(ref concurrencyCounter);
            });
            
            //Debug.WriteLine($"Max Concurrency: {maxConcurrency}");
            var averages = results.ToDictionary(k => k.Key, v => v.Value.Average());
            var min = averages.Values.Min();
            Console.WriteLine($"Averages calculated in {sw.ElapsedMilliseconds}ms");
            return averages
                .Where(kvp => kvp.Value == min)
                .Select(kvp => kvp.Key);
        }

        public async Task<IEnumerable<string>> CalculateWordAsync(Wordle wordle)
        {
            var sw = Stopwatch.StartNew();
            var currentWordList = wordle.filtered;
            var results = new ConcurrentDictionary<string, List<int>>();
            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = _maxDop
            };
            await Parallel.ForEachAsync(currentWordList.ToArray(), options, async (guess, ct) =>
            {
                //int concurrency = Interlocked.Increment(ref concurrencyCounter);
                //lock (locker) maxConcurrency = Math.Max(maxConcurrency, concurrency);
                results[guess] = new List<int>();
                foreach (string potentialAnswer in currentWordList)
                {
                    //if (guess == potentialAnswer) continue;
                    var scoredWord = Wordle.ScoreWord(guess, potentialAnswer);
                    results[guess].Add(await Task.Run(() =>
                        ProcessHypothetical(
                            scoredWord,
                            wordle.filtered.ToArray(),
                            wordle.positionExcluded.ToArray(),
                            wordle.possibles.ToArray(),
                            wordle.globalExcluded.ToList())
                        ));
                }
                //Interlocked.Decrement(ref concurrencyCounter);
            });
            var averages = results.ToDictionary(k => k.Key, v => v.Value.Average());
            var min = averages.Values.Min();
            Console.WriteLine($"Averages calculated in {sw.ElapsedMilliseconds}ms");
            return averages
                .Where(kvp => kvp.Value == min)
                .Select(kvp => kvp.Key);
        }

        /// <summary>
        /// A streamlined version of the <see cref="Wordle.ProcessGuess(string)"/> method that returns the size of the resultant word list based on a hypothetical guess
        /// </summary>
        /// <param name="guess">The hypothetical guess</param>
        /// <param name="filtered">The current state of the filtered word list</param>
        /// <param name="regexArray">The current state of the regex array</param>
        /// <param name="positionExcluded">the current state of the positionExcluded values</param>
        /// <param name="possibles">The current state of the possible character values</param>
        /// <param name="globalExcluded">The current state of the global excluded character values</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal int ProcessHypothetical(string guess, string[] filtered, string[] positionExcluded, string[] possibles, List<char> globalExcluded)
        {
            var loopExclude = new char[5];
            var loopPossible = new char[5];
            var regexArray = new string[5];
            for (var i = 0; i < 5; i++)
            {
                string substring = guess.Substring(2 * i, 2);

                switch (int.Parse(substring[1].ToString()))
                {
                    case 0:
                        {
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
                string currentRegexEntry = regexArray[i];
                if (currentRegexEntry != null && currentRegexEntry.Length == 1 && AToZ.Contains(currentRegexEntry[0])) { continue; }

                var excluded = (
                        positionExcluded[i] +
                        possibles[i] +
                        new string(globalExcluded.ToArray()))
                    .ToCharArray()
                    .Distinct()
                    .Where(c => AToZ.Contains(c));
                    //.Where(c => Regex.IsMatch(c.ToString(), "[a-z]"));
                regexArray[i] = $"[^{string.Join("", excluded)}]";
            }

            filtered = filtered
                .Where(word => Regex.IsMatch(word, string.Join("", regexArray)))
                .ToArray();

            foreach (char letter in
                        string.Join("", possibles)
                         .ToCharArray()
                         .Where(c => AToZ.Contains(c))
                         //.Where(c => Regex.IsMatch(c.ToString(), "[a-z]"))
                         .Distinct())
            {
                filtered = filtered.Where(f => f.Contains(letter)).ToArray();
            }
            return filtered.Length;
        }
    }
}

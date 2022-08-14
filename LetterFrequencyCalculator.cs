using System;
using System.Collections.Generic;
using System.Linq;

namespace WordleSharp.Calculators
{
    public class LetterFrequencyCalculator : INextWordCalculator
    {
        public IEnumerable<string> CalculateWord(IEnumerable<string> wordList)
        {
            if (!wordList.Any())
            {
                Console.WriteLine($"{GetType().Name} Error! Word list contained no entries!");
                return Enumerable.Empty<string>();
            }
            var dict = string.Join("", wordList)
                .ToCharArray()
                .GroupBy(x => x)
                .ToDictionary(k => k.Key, v => v.Count());
            var scores = new Dictionary<string, int>();
            foreach (string word in wordList)
            {
                int sum = word
                    .ToCharArray()
                    .Distinct()
                    .Sum(letter => dict[letter]);
                scores[word] = sum;
            }

            int lowestScore = scores.Max(x => x.Value);
            return scores.Where(x => x.Value == lowestScore).Select(s => s.Key);
        }
    }
}
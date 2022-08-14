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
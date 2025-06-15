using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WordleSharp.Calculators
{
    /// <summary>
    /// Calculates next word by analysing the frequency of the letters in the current remaining word list,
    /// then scores each word according to that frequency distribution (multiple counts of the same letter
    /// are only scored once). The word or words with the highest score are returned.
    /// </summary>
    public class LetterFrequencyCalculator : INextWordCalculator
    {
        public IEnumerable<string> CalculateWord(Wordle wordle)
        {
            var wordList = wordle.filtered;
            if (!wordList.Any())
            {
                Console.WriteLine($"{GetType().Name} Error! Word list contained no entries!");
                return Enumerable.Empty<string>();
            }
            var characterFrequency = string.Join("", wordList)
                .ToCharArray()
                .GroupBy(x => x)
                .ToDictionary(k => k.Key, v => v.Count());
            var wordScores = new Dictionary<string, int>();
            foreach (string word in wordList)
            {
                int sum = word
                    .ToCharArray()
                    .Distinct()
                    .Sum(letter => characterFrequency[letter]);
                wordScores[word] = sum;
            }

            int highestScore = wordScores.Max(x => x.Value);
            return wordScores.Where(x => x.Value == highestScore).Select(s => s.Key);
        }

        public Task<IEnumerable<string>> CalculateWordAsync(Wordle wordle)
        {
            throw new NotImplementedException();
        }
    }
}
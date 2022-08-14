using System.Collections.Generic;

namespace WordleSharp.Calculators
{
    public interface INextWordCalculator
    {
        public IEnumerable<string> CalculateWord(IEnumerable<string> wordList);
    }
}
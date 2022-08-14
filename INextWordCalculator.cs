using System.Collections.Generic;

namespace WordleSharp
{
    public interface INextWordCalculator
    {
        public IEnumerable<string> CalculateWord(IEnumerable<string> wordList);
    }
}
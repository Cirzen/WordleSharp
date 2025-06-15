using System.Collections.Generic;
using System.Threading.Tasks;

namespace WordleSharp.Calculators
{
    public interface INextWordCalculator
    {
        /// <summary>
        /// Calculates the best next word using the current calculator
        /// </summary>
        /// <param name="wordle">The Wordle object</param>
        /// <returns></returns>
        public IEnumerable<string> CalculateWord(Wordle wordle);

        public Task<IEnumerable<string>> CalculateWordAsync(Wordle wordle);
    }
}
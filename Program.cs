using System;
using WordleSharp.Calculators;

namespace WordleSharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var wordle = new Wordle();
            wordle.SetNextWordCalculator(new LetterFrequencyCalculator());
            var result = wordle.Analyse();
            Console.WriteLine(result);
        }
    }
}

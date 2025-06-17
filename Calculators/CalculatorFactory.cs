namespace WordleSharp.Calculators;

public static class CalculatorFactory
{
    public static INextWordCalculator CreateCalculator(CalculatorType calculatorType, int? maxDop = null)
    {
        return calculatorType switch
        {
            CalculatorType.CountReduction => new CountReductionCalculator(maxDop),
            CalculatorType.LetterFrequency => new LetterFrequencyCalculator(),
            _ => new CountReductionCalculator(maxDop)
        };
    }
}

public enum CalculatorType
{
    CountReduction,
    LetterFrequency
}

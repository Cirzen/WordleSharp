namespace WordleSharp.Calculators;

internal class FilteringCriteria
{
    public string?[] RegexArray { get; set; }
    public List<char> GlobalExcluded { get; set; }
    public string?[] PositionExcluded { get; set; }
    public List<char> MustBePresentChars { get; set; }

    // Constructor for initializing with a specific capacity or default
    public FilteringCriteria(bool initializeCollections = true)
    {
        if (initializeCollections)
        {
            RegexArray = new string[5];
            GlobalExcluded = new List<char>(26);
            PositionExcluded = new string[5];
            MustBePresentChars = new List<char>(5);
        }
        else // Needed for cases where properties are set from elsewhere (e.g. cloning)
        {
            RegexArray = null!; // Will be set by cloner
            GlobalExcluded = null!; // Will be set by cloner
            PositionExcluded = null!; // Will be set by cloner
            MustBePresentChars = null!; // Will be set by cloner
        }
    }

    // Clone constructor
    public FilteringCriteria(FilteringCriteria source)
    {
        RegexArray = (string?[])source.RegexArray.Clone();
        GlobalExcluded = new List<char>(source.GlobalExcluded);
        PositionExcluded = (string?[])source.PositionExcluded.Clone();
        MustBePresentChars = new List<char>(source.MustBePresentChars);
    }
        
    // Constructor from individual base elements (for initial setup)
    public FilteringCriteria(
        string[] baseRegexArray,
        List<char> baseGlobalExcluded,
        string[] basePositionExcluded,
        List<char> baseMustBePresentChars)
    {
        RegexArray = (string?[])baseRegexArray.Clone();
        GlobalExcluded = new List<char>(baseGlobalExcluded);
        PositionExcluded = (string?[])basePositionExcluded.Clone();
        MustBePresentChars = new List<char>(baseMustBePresentChars);
    }
}
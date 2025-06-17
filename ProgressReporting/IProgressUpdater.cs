namespace WordleSharp.ProgressReporting;

public interface IProgressUpdater
{
    void StartProgress(int totalItems, string initialMessage);
    void UpdateProgress(int currentItems, string message);
    void IncrementProgress(string perItemMessageFormat = "Processed {0}/{1}"); // Allows customizing message per item
    void CompleteProgress(string finalMessage);
}
using System.Text;

namespace WordleSharp.ProgressReporting;

public class ConsoleProgressUpdater : IProgressUpdater
{
    private const int BarWidth = 30;
    private string _currentActivityMessage = "";
    private int _totalItems;
    private int _processedItems;
    private readonly object _lock = new();
    private int _lastConsoleLineLength;
    private bool _isStarted;
    private bool _isCompleted;

    public void StartProgress(int totalItems, string initialMessage)
    {
        lock (_lock)
        {
            if (_isStarted)
            {
                return;
            }

            _totalItems = totalItems;
            _processedItems = 0;
            _currentActivityMessage = initialMessage;
            _isStarted = true;
            _isCompleted = false;
            _lastConsoleLineLength = 0;
            DrawProgress();
        }
    }

    public void UpdateProgress(int currentItems, string message)
    {
        lock (_lock)
        {
            if (!_isStarted || _isCompleted)
            {
                return;
            }

            _processedItems = Math.Min(currentItems, _totalItems);
            _currentActivityMessage = message;
            DrawProgress();
        }
    }

    public void IncrementProgress(string perItemMessageFormat = "Processed {0}/{1}")
    {
        lock (_lock)
        {
            if (!_isStarted || _isCompleted)
            {
                return;
            }

            _processedItems = Math.Min(_processedItems + 1, _totalItems);
            _currentActivityMessage = string.Format(perItemMessageFormat, _processedItems, _totalItems);
            DrawProgress();
        }
    }

    private void DrawProgress()
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        ClearConsoleLine();

        double percentage =
            (_totalItems == 0) ? 1.0 : Math.Max(0, Math.Min(1.0, (double)_processedItems / _totalItems));
        int filledWidth = (int)(percentage * BarWidth);
        int emptyWidth = BarWidth - filledWidth;

        var sb = new StringBuilder();
        sb.Append(_currentActivityMessage);
        sb.Append(" [");
        sb.Append(new string('#', filledWidth));
        sb.Append(new string('-', emptyWidth));
        sb.Append($"] {_processedItems}/{_totalItems} ({(int)(percentage * 100)}%)");

        string output = sb.ToString();
        Console.Write(output);
        _lastConsoleLineLength = output.Length;
    }


    public void CompleteProgress(string finalMessage)
    {
        lock (_lock)
        {
            if (!_isStarted || _isCompleted)
            {
                return;
            }

            _isCompleted = true;

            if (Console.IsOutputRedirected)
            {
                Console.WriteLine(finalMessage);
                return;
            }

            ClearConsoleLine();

            Console.WriteLine(finalMessage);
            _lastConsoleLineLength = 0;
        }
    }
    
    /// <summary>
    /// Backtracks the cursor to the start of the line and clears current contents
    /// </summary>
    private void ClearConsoleLine()
    {
        Console.Write("\r" + new string(' ', _lastConsoleLineLength) + "\r");
    }
}
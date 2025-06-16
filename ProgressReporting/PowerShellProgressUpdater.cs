using System.Diagnostics;
using System.Management.Automation;

namespace WordleSharp.ProgressReporting;

/// <summary>
/// PowerShell-specific progress reporter that uses Cmdlet.WriteProgress.
/// Only shows progress if the operation exceeds a configured threshold.
/// </summary>
public class PowerShellProgressUpdater : IProgressUpdater
{
    private readonly PSCmdlet _cmdlet;
    private readonly int _activityId;
    private readonly string _activity;
    private readonly TimeSpan _progressThreshold;
    private readonly Stopwatch _stopwatch;
    
    private int _totalItems;
    private int _processedItems;
    private bool _isStarted;
    private bool _isCompleted;
    private bool _shouldShowProgress;

    /// <summary>
    /// Creates a new PowerShell progress updater.
    /// </summary>
    /// <param name="cmdlet">The cmdlet that will display the progress</param>
    /// <param name="activity">The activity description</param>
    /// <param name="activityId">Unique activity ID for this progress operation</param>
    /// <param name="progressThresholdMs">Minimum time in milliseconds before showing progress (default: 500ms)</param>
    public PowerShellProgressUpdater(PSCmdlet cmdlet, string activity, int activityId = 1, int progressThresholdMs = 500)
    {
        _cmdlet = cmdlet ?? throw new ArgumentNullException(nameof(cmdlet));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _activityId = activityId;
        _progressThreshold = TimeSpan.FromMilliseconds(progressThresholdMs);
        _stopwatch = new Stopwatch();
    }

    public void StartProgress(int totalItems, string initialMessage)
    {
        if (_isStarted)
        {
            return;
        }

        _totalItems = totalItems;
        _processedItems = 0;
        _isStarted = true;
        _isCompleted = false;
        _shouldShowProgress = false;
        
        _stopwatch.Start();
        
        // Don't show progress immediately - wait for threshold
        CheckAndShowProgress(initialMessage);
    }

    public void UpdateProgress(int currentItems, string message)
    {
        if (!_isStarted || _isCompleted)
        {
            return;
        }

        _processedItems = Math.Min(currentItems, _totalItems);
        CheckAndShowProgress(message);
    }

    public void IncrementProgress(string perItemMessageFormat = "Processed {0}/{1}")
    {
        if (!_isStarted || _isCompleted)
        {
            return;
        }

        _processedItems = Math.Min(_processedItems + 1, _totalItems);
        var message = string.Format(perItemMessageFormat, _processedItems, _totalItems);
        CheckAndShowProgress(message);
    }

    public void CompleteProgress(string finalMessage)
    {
        if (!_isStarted || _isCompleted)
        {
            return;
        }

        _isCompleted = true;
        _stopwatch.Stop();

        // Only show completion if we were showing progress
        if (_shouldShowProgress)
        {
            var progressRecord = new ProgressRecord(_activityId, _activity, finalMessage)
            {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed
            };
            
            _cmdlet.WriteProgress(progressRecord);
        }
    }

    private void CheckAndShowProgress(string statusDescription)
    {
        // Check if we should start showing progress
        if (!_shouldShowProgress && _stopwatch.Elapsed >= _progressThreshold)
        {
            _shouldShowProgress = true;
        }

        // Only show progress if threshold is exceeded
        if (_shouldShowProgress)
        {
            var percentComplete = _totalItems > 0 ? (int)((double)_processedItems / _totalItems * 100) : 0;
            
            var progressRecord = new ProgressRecord(_activityId, _activity, statusDescription)
            {
                PercentComplete = percentComplete,
                RecordType = ProgressRecordType.Processing
            };

            // Add current/total info to the status
            if (_totalItems > 0)
            {
                progressRecord.StatusDescription = $"{statusDescription} ({_processedItems}/{_totalItems})";
            }

            _cmdlet.WriteProgress(progressRecord);
        }
    }
}

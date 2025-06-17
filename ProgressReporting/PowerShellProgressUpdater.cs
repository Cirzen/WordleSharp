using System.Diagnostics;
using System.Management.Automation;
using System.Collections.Concurrent;

namespace WordleSharp.ProgressReporting;

/// <summary>
/// Represents a progress update request that can be queued for thread-safe processing.
/// </summary>
public class ProgressUpdateRequest
{
    public ProgressRecord ProgressRecord { get; set; }
    public DateTime Timestamp { get; set; }
    
    public ProgressUpdateRequest(ProgressRecord progressRecord)
    {
        ProgressRecord = progressRecord;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// PowerShell-specific progress reporter that uses Cmdlet.WriteProgress.
/// Only shows progress if the operation exceeds a configured threshold.
/// Queues progress updates for processing on the main thread.
/// </summary>
public class PowerShellProgressUpdater : IProgressUpdater, IDisposable
{
    private readonly PSCmdlet _cmdlet;
    private readonly int _activityId;
    private readonly string _activity;
    private readonly TimeSpan _progressThreshold;
    private readonly Stopwatch _stopwatch;
    private readonly object _lock = new object();
    private readonly ConcurrentQueue<ProgressUpdateRequest> _progressQueue;
    
    private int _totalItems;
    private int _processedItems;
    private bool _isStarted;
    private bool _isCompleted;
    private bool _shouldShowProgress;
    private volatile bool _disposed;
    private ProgressRecord? _lastProgressRecord;    /// <summary>
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
        _progressQueue = new ConcurrentQueue<ProgressUpdateRequest>();
    }

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
            _isStarted = true;
            _isCompleted = false;
            _shouldShowProgress = false;
            
            _stopwatch.Start();
            
            // Queue initial progress update
            QueueProgressUpdate(initialMessage);
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
            QueueProgressUpdate(message);
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
            var message = string.Format(perItemMessageFormat, _processedItems, _totalItems);
            QueueProgressUpdate(message);
        }
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
            _stopwatch.Stop();

            // Queue final progress update if we were showing progress
            if (_shouldShowProgress)
            {
                var progressRecord = new ProgressRecord(_activityId, _activity, finalMessage)
                {
                    PercentComplete = 100,
                    RecordType = ProgressRecordType.Completed
                };
                
                _progressQueue.Enqueue(new ProgressUpdateRequest(progressRecord));
            }
        }
    }

    private void QueueProgressUpdate(string statusDescription)
    {
        // Check if we should start showing progress
        if (!_shouldShowProgress && _stopwatch.Elapsed >= _progressThreshold)
        {
            _shouldShowProgress = true;
        }

        // Only queue progress if threshold is exceeded
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

            _progressQueue.Enqueue(new ProgressUpdateRequest(progressRecord));
        }
    }    private void ProcessProgressUpdates(object? state)
    {
        if (_disposed)
            return;

        // Process queued progress updates
        // Collect all updates and only process the most recent to avoid flooding
        var updates = new List<ProgressUpdateRequest>();
        
        while (_progressQueue.TryDequeue(out var request))
        {
            updates.Add(request);
        }

        if (updates.Count > 0)
        {
            try
            {
                // Only show the most recent update to avoid flooding the UI
                var latestUpdate = updates[updates.Count - 1];
                _cmdlet.WriteProgress(latestUpdate.ProgressRecord);
                _lastProgressRecord = latestUpdate.ProgressRecord;
            }
            catch (ObjectDisposedException)
            {
                // Cmdlet has been disposed, stop processing
                _disposed = true;
            }
            catch (InvalidOperationException)
            {
                // If we still have threading issues, store the latest update
                // The main thread can process it later by calling ProcessQueuedUpdates
                if (updates.Count > 0)
                {
                    _progressQueue.Enqueue(updates[updates.Count - 1]);
                }
            }
        }
    }

    /// <summary>
    /// Process any queued progress updates on the calling thread.
    /// Should be called from the main cmdlet thread.
    /// </summary>
    public void ProcessQueuedUpdates()
    {
        if (_disposed)
            return;

        while (_progressQueue.TryDequeue(out var request))
        {
            try
            {
                _cmdlet.WriteProgress(request.ProgressRecord);
                _lastProgressRecord = request.ProgressRecord;
            }
            catch (ObjectDisposedException)
            {
                _disposed = true;
                break;
            }
            catch (InvalidOperationException)
            {
                // Still on wrong thread, re-queue for later
                _progressQueue.Enqueue(request);
                break;
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        
        // Clear any remaining progress updates
        while (_progressQueue.TryDequeue(out _)) { }
    }
}

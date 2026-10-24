using VepayDO.Application.Configuration;

namespace VepayDO.Worker;

/// <summary>
/// The knobs of the receipt worker (M5.4). They come from the same validated
/// configuration as the API, so a deployment tunes them in one place.
/// </summary>
public sealed record WorkerSettings
{
    public WorkerSettings(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        BatchSize = config.WorkerBatchSize;
        PollIntervalSeconds = config.WorkerPollIntervalSeconds;
        MaxAttempts = config.WorkerMaxAttempts;
    }

    /// <summary>How many jobs are claimed per cycle.</summary>
    public int BatchSize { get; }

    /// <summary>How long the worker waits between cycles when there is nothing to do.</summary>
    public int PollIntervalSeconds { get; }

    /// <summary>How many times a job is retried before it is parked as failed.</summary>
    public int MaxAttempts { get; }
}

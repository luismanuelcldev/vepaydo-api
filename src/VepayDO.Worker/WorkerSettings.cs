using VepayDO.Application.Configuration;

namespace VepayDO.Worker;

public sealed record WorkerSettings
{
    public WorkerSettings(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        BatchSize = config.WorkerBatchSize;
        PollIntervalSeconds = config.WorkerPollIntervalSeconds;
        MaxAttempts = config.WorkerMaxAttempts;
    }

    public int BatchSize { get; }

    public int PollIntervalSeconds { get; }

    public int MaxAttempts { get; }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using VepayDO.Application.Receipts;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Application.Tenancy;

namespace VepayDO.Worker;

/// <summary>
/// Drains the <c>processing_jobs</c> queue: every claimed job is processed
/// inside the tenant transaction, as the <c>system</c> role (M5.4).
///
/// <para>
/// The worker never decides the outcome of a receipt: it moves it through the
/// processing axis and leaves the reconciliation axis to the human (AGENTS.md
/// section 5). A job that fails is released with a backoff; it is never
/// silently dropped.
/// </para>
/// </summary>
public sealed class ReceiptProcessingWorker(
    IServiceScopeFactory scopeFactory,
    WorkerSettings settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information(
            "Receipt worker started (batch {BatchSize}, poll {PollSeconds}s, max attempts {MaxAttempts}).",
            settings.BatchSize,
            settings.PollIntervalSeconds,
            settings.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;

            try
            {
                claimed = await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "The receipt worker cycle failed.");
            }

            if (claimed == 0)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(settings.PollIntervalSeconds),
                        stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        Log.Information("Receipt worker stopped.");
    }

    private async Task<int> RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IReceiptJobReader>();

        var jobs = await reader.ClaimAsync(settings.BatchSize, cancellationToken).ConfigureAwait(false);
        if (jobs.Count == 0)
        {
            return 0;
        }

        foreach (var job in jobs)
        {
            await ProcessJobAsync(job, reader, cancellationToken).ConfigureAwait(false);
        }

        return jobs.Count;
    }

    private async Task ProcessJobAsync(
        ReceiptJob job,
        IReceiptJobReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<ITenantTransactionRunner>();
            var processor = scope.ServiceProvider.GetRequiredService<IReceiptProcessingService>();

            var outcome = await runner.RunAsSystemAsync(
                job.TenantId,
                async token => await processor.ProcessAsync(job.Id, token).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            Log.Information(
                "Receipt job {JobId} processed ({Outcome}).",
                job.Id,
                outcome);

            await reader.CompleteAsync(job.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var attempts = job.Attempts + 1;
            var delay = Backoff(attempts);
            var exhausted = attempts >= settings.MaxAttempts;

            Log.Error(
                exception,
                "Receipt job {JobId} failed on attempt {Attempt} of {MaxAttempts}{Retry}.",
                job.Id,
                attempts,
                settings.MaxAttempts,
                exhausted ? string.Empty : $"; retrying in {delay.TotalSeconds:0}s");

            await reader
                .FailAsync(job.Id, exception.Message, delay, settings.MaxAttempts, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Exponential backoff, capped at five minutes so a stuck job keeps being
    /// retried without hammering the database.
    /// </summary>
    private static TimeSpan Backoff(int attempts)
    {
        var seconds = Math.Min(300, 5 * Math.Pow(2, Math.Max(0, attempts - 1)));
        return TimeSpan.FromSeconds(seconds);
    }
}

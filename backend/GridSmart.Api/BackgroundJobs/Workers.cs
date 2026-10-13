using System.Threading.Channels;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GridSmart.Api.BackgroundJobs;

/// <summary>Hang doi trong bo nho cho pipeline nen (BE-12).</summary>
public sealed class InMemoryIncidentQueue : IIncidentQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(long incidentId) => _channel.Writer.TryWrite(incidentId);

    public IAsyncEnumerable<long> DequeueAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>Tieu thu hang doi va xu ly tung su co mot (BE-12).</summary>
public sealed class IncidentPipelineWorker(IIncidentQueue queue, IServiceScopeFactory scopes, ILogger<IncidentPipelineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IIncidentPipeline>().ProcessAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Khong xu ly duoc su co {IncidentId}, se thu lai o lan khoi phuc", id);
            }
        }
    }
}

/// <summary>Khi khoi dong va dinh ky: nap lai su co chua di het pipeline de khong mat viec (BE-12).</summary>
public sealed class PipelineRecoveryWorker(
    IIncidentQueue queue, IServiceScopeFactory scopes, IOptions<PipelineOptions> options, TimeProvider clock,
    ILogger<PipelineRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var first = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var incidents = scope.ServiceProvider.GetRequiredService<IncidentsDataAccess>();
                // Lan dau sau khi khoi dong: lay ca su co vua tao (co the bi bo do tat may)
                var cutoff = clock.GetUtcNow().UtcDateTime.AddMinutes(first ? 0 : -options.Value.RecoveryMinAgeMinutes);
                var ids = (await incidents.ReadPendingPipelineAsync(cutoff, 500)).Select(r => r.GetLong("id")).ToList();
                foreach (var id in ids) queue.Enqueue(id);
                if (ids.Count > 0) logger.LogInformation("Khoi phuc pipeline cho {Count} su co", ids.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Loi khi khoi phuc pipeline");
            }
            first = false;
            await Task.Delay(TimeSpan.FromMinutes(options.Value.RecoveryIntervalMinutes), clock, stoppingToken).ContinueWith(_ => { });
        }
    }
}

/// <summary>Chay SLA Engine dinh ky (BE-20).</summary>
public sealed class SlaWorker(IServiceScopeFactory scopes, IOptions<SlaOptions> options, TimeProvider clock, ILogger<SlaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISlaEngine>().RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Loi khi quet SLA");
            }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.ScanIntervalSeconds), clock, stoppingToken).ContinueWith(_ => { });
        }
    }
}

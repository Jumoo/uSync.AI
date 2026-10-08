using Jumoo.Processing.Core.Pipelines;
using Jumoo.Processing.Core.Pipelines.Models;
using Jumoo.Processing.Core.Processing.Interfaces;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.Membership;
using uSync.AI.Tools.Services;

namespace uSync.AI.Complete.Services;

/// <summary>What a uSync.Complete tool hands back to the model.</summary>
public sealed record uSyncCompleteToolResult(string Operation, bool Success, string? Message, string? Server = null)
{
    public static uSyncCompleteToolResult Failed(string operation, string message, string? server = null)
        => new(operation, false, message, server);
}

/// <summary>
/// Runs a uSync.Complete processing pipeline (publisher push or pull, restore point) to the end.
/// </summary>
public interface IuSyncCompletePipelineRunner
{
    Task<uSyncCompleteToolResult> RunAsync(
        string operation, string pipelineAlias, string strategyAlias, IProcessingOptions options,
        IUser user, string? server, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IuSyncCompletePipelineRunner"/>
/// <remarks>
/// The create, update options, process-until-done loop follows <c>ProcessingRunner</c> in
/// uSync.Automate.Actions.Complete, with one deliberate difference: the pipeline runs as the
/// user the agent is acting for, not the super user. uSync.Publisher checks push and pull
/// permission against the user it is given, so the user's own permissions apply all the way down.
/// It shares the operation gate with the local uSync tools, so a publish can't overlap an import.
/// </remarks>
public sealed class uSyncCompletePipelineRunner : IuSyncCompletePipelineRunner
{
    /// <summary>A guard against a pipeline that never stops reporting itself as running.</summary>
    private const int MaxSteps = 1000;

    private readonly IPipelineService _pipelineService;
    private readonly IuSyncOperationGate _gate;
    private readonly ILogger<uSyncCompletePipelineRunner> _logger;

    public uSyncCompletePipelineRunner(IPipelineService pipelineService, IuSyncOperationGate gate, ILogger<uSyncCompletePipelineRunner> logger)
    {
        _pipelineService = pipelineService;
        _gate = gate;
        _logger = logger;
    }

    public async Task<uSyncCompleteToolResult> RunAsync(
        string operation, string pipelineAlias, string strategyAlias, IProcessingOptions options,
        IUser user, string? server, CancellationToken cancellationToken)
    {
        using var lease = _gate.TryAcquire();
        if (lease is null)
            return uSyncCompleteToolResult.Failed(operation, "Another uSync operation is already running. Try again when it has finished.", server);

        try
        {
            _logger.LogInformation("uSync.Complete {Operation} started by an AI agent for {User} (server {Server})",
                operation, user.Username, server ?? "(none)");

            var pipeline = await _pipelineService.CreatePipeline(new CreatePipelineOptions
            {
                Alias = pipelineAlias,
                Strategy = strategyAlias,
                IsInBackground = true,
                User = user,
            });

            await _pipelineService.UpdateOptions(pipeline.Id, options, user);

            IPipeline current = await _pipelineService.GetPipeline(pipeline.Id, user)
                ?? throw new InvalidOperationException($"Pipeline {pipeline.Id} was not found after it was created.");

            var steps = 0;
            while (steps < MaxSteps && current.State.Status == PipelineStatus.Running)
            {
                if (cancellationToken.IsCancellationRequested) break;

                steps++;
                current = await _pipelineService.Process(pipeline.Id, user, clientId: string.Empty, isBackgroundRequest: true, cancellationToken);
            }

            if (current.State.Status == PipelineStatus.Completed)
                return new uSyncCompleteToolResult(operation, true, $"Completed in {steps} step{(steps == 1 ? string.Empty : "s")}.", server);

            // the pipeline's own error text is written for the backoffice user, so it is safe to pass on.
            return uSyncCompleteToolResult.Failed(operation,
                current.Results?.Error?.Message ?? $"The {operation} stopped with status {current.State.Status}.", server);
        }
        catch (Exception ex)
        {
            // the full exception goes to the log; the model gets only that it failed.
            _logger.LogError(ex, "uSync.Complete {Operation} run by an AI agent for {User} failed", operation, user.Username);
            return uSyncCompleteToolResult.Failed(operation, $"The {operation} failed. The site log has the details.", server);
        }
    }
}

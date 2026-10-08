using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using uSync.AI.Tools.Models;
using uSync.AI.Tools.Security;
using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Models;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers.Models;
using uSync.Core;
using uSyncClass = uSync.BackOffice.uSync;

namespace uSync.AI.Tools.Services;

/// <summary>One bulk uSync operation asked for by a tool.</summary>
public sealed record uSyncToolRunRequest(HandlerActions Action, string? Group = null, string[]? Handlers = null, bool Force = false);

/// <summary>
/// Runs a uSync report, export or import for an agent tool: authorizes the acting user, takes
/// the operation gate, then performs the same start, per-handler, post, finish sequence uSync's
/// management API does. Every tool goes through here, so no tool can run uSync unauthorized.
/// </summary>
/// <remarks>
/// The sequence follows <c>uSyncRunner</c> in uSync.Automate.Actions. None of uSync's own APIs
/// accept a cancellation token, so cancellation is only observed between handlers.
/// </remarks>
public sealed class uSyncToolRunner
{
    private readonly ISyncActionService _actionService;
    private readonly ISyncConfigService _configService;
    private readonly IuSyncToolAuthorizer _authorizer;
    private readonly IuSyncOperationGate _gate;
    private readonly IOptionsMonitor<uSyncAIToolsOptions> _options;
    private readonly ILogger<uSyncToolRunner> _logger;

    public uSyncToolRunner(
        ISyncActionService actionService,
        ISyncConfigService configService,
        IuSyncToolAuthorizer authorizer,
        IuSyncOperationGate gate,
        IOptionsMonitor<uSyncAIToolsOptions> options,
        ILogger<uSyncToolRunner> logger)
    {
        _actionService = actionService;
        _configService = configService;
        _authorizer = authorizer;
        _gate = gate;
        _options = options;
        _logger = logger;
    }

    public static uSyncToolAccess AccessFor(HandlerActions action) => action switch
    {
        HandlerActions.Import => uSyncToolAccess.Import,
        HandlerActions.Export => uSyncToolAccess.Export,
        _ => uSyncToolAccess.Read,
    };

    /// <summary>The handlers that would take part in <paramref name="action"/>, for the acting user.</summary>
    public uSyncToolHandlerList ListHandlers(HandlerActions action, string? group)
    {
        var authorization = _authorizer.Authorize(uSyncToolAccess.Read);
        if (!authorization.IsAuthorized) return new uSyncToolHandlerList(false, authorization.Message, []);

        var handlers = GetHandlers(action, group, _configService.Settings.DefaultSet, force: false, aliases: null);
        return new uSyncToolHandlerList(true, null, [.. handlers.Select(x => new uSyncToolHandler(x.Alias, x.Name, x.Group, x.Enabled))]);
    }

    public async Task<uSyncToolResult> RunAsync(uSyncToolRunRequest request, CancellationToken cancellationToken)
    {
        var operation = request.Action.ToString().ToLowerInvariant();

        var authorization = _authorizer.Authorize(AccessFor(request.Action));
        if (!authorization.IsAuthorized)
        {
            _logger.LogWarning("uSync {Operation} tool refused for {User}: {Reason}",
                operation, authorization.User?.Username ?? "(no user)", authorization.Message);
            return uSyncToolResult.Failed(operation, authorization.Message!);
        }

        using var lease = _gate.TryAcquire();
        if (lease is null)
            return uSyncToolResult.Failed(operation, "Another uSync operation is already running. Try again when it has finished.");

        var username = authorization.User!.Username;

        try
        {
            var group = string.IsNullOrWhiteSpace(request.Group) ? uSyncClass.EverythingGroupName : request.Group;
            var set = _configService.Settings.DefaultSet;
            var folders = _configService.GetFolders();

            var handlers = GetHandlers(request.Action, group, set, request.Force, request.Handlers);
            if (handlers.Count == 0)
                return uSyncToolResult.Failed(operation, "No uSync handlers matched. Use usync_list_handlers to see what is available.");

            _logger.LogInformation("uSync {Operation} started by an AI agent for {User} ({Count} handlers, group {Group})",
                operation, username, handlers.Count, group);

            await _actionService.StartProcessAsync(new SyncStartActionRequest { HandlerAction = request.Action, Username = username });

            var handlerMethod = GetHandlerMethod(request.Action);
            var actions = new List<uSyncAction>();

            foreach (var handler in handlers)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var result = await handlerMethod(
                    new SyncActionOptions { Folders = folders, Set = set, Group = group, Force = request.Force, Handler = handler.Alias }, null);
                actions = actions.Merge(result.Actions);
            }

            var finalRequest = new SyncFinalActionRequest
            {
                RequestId = Guid.NewGuid(),
                HandlerAction = request.Action,
                ActionOptions = new SyncActionOptions { Folders = folders, Set = set, Group = group, Force = request.Force },
                Actions = actions,
                Username = username,
            };

            if (request.Action == HandlerActions.Import)
            {
                var postResult = await _actionService.ImportPostAsync(finalRequest);
                actions = actions.Merge(postResult.Actions);
                finalRequest.Actions = actions;
            }

            var finishResult = await _actionService.FinishProcessAsync(finalRequest);
            actions = actions.Merge(finishResult.Actions);

            var visible = actions.Where(x => x.Change != ChangeType.Hidden).ToList();
            return uSyncToolResult.From(operation, visible, _options.CurrentValue.MaxChanges);
        }
        catch (Exception ex)
        {
            // the full exception goes to the log; the model gets only that it failed.
            _logger.LogError(ex, "uSync {Operation} run by an AI agent for {User} failed", operation, username);
            return uSyncToolResult.Failed(operation, $"The uSync {operation} failed. The site log has the details.");
        }
    }

    private List<SyncHandlerView> GetHandlers(HandlerActions action, string? group, string set, bool force, string[]? aliases)
    {
        var handlers = _actionService.GetActionHandlers(action, new uSyncOptions
        {
            Group = string.IsNullOrWhiteSpace(group) ? uSyncClass.EverythingGroupName : group,
            Set = set,
            Force = force,
        });

        return aliases is { Length: > 0 }
            ? [.. handlers.Where(x => aliases.Contains(x.Alias, StringComparer.OrdinalIgnoreCase))]
            : [.. handlers];
    }

    private Func<SyncActionOptions, uSyncCallbacks?, Task<SyncActionResult>> GetHandlerMethod(HandlerActions action) => action switch
    {
        HandlerActions.Import => _actionService.ImportHandlerAsync,
        HandlerActions.Export => _actionService.ExportHandlerAsync,
        HandlerActions.Report => _actionService.ReportHandlerAsync,
        _ => throw new InvalidOperationException($"{action} is not supported."),
    };
}

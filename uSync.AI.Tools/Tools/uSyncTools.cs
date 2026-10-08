using System.ComponentModel;
using Umbraco.AI.Core.Tools;
using Umbraco.AI.Core.Tools.Scopes;
using uSync.AI.Tools.Services;
using uSync.BackOffice.SyncHandlers.Models;

namespace uSync.AI.Tools.Tools;

/// <summary>Read-only uSync tools: reports and listings.</summary>
[AIToolScope(ScopeId, Icon = "icon-infinity", Domain = uSyncToolScopes.Domain)]
public class uSyncReadScope : AIToolScopeBase
{
    public const string ScopeId = "usync-read";
}

/// <summary>uSync tools that write files to disk or change the site.</summary>
[AIToolScope(ScopeId, Icon = "icon-infinity", Domain = uSyncToolScopes.Domain, IsDestructive = true)]
public class uSyncWriteScope : AIToolScopeBase
{
    public const string ScopeId = "usync-write";
}

public static class uSyncToolScopes
{
    public const string Domain = "uSync";
}

public record uSyncListHandlersArgs(
    [property: Description("Optional handler group to list, e.g. 'Settings', 'Content' or 'AI'. Leave empty for every group.")]
    string? Group = null);

public record uSyncRunArgs(
    [property: Description("Optional handler group to run, e.g. 'Settings', 'Content' or 'AI'. Leave empty to run everything.")]
    string? Group = null,
    [property: Description("Optional handler aliases to limit the run to. Use aliases from usync_list_handlers.")]
    string[]? Handlers = null);

public record uSyncImportArgs(
    [property: Description("Optional handler group to import, e.g. 'Settings', 'Content' or 'AI'. Leave empty to import everything.")]
    string? Group = null,
    [property: Description("Optional handler aliases to limit the import to. Use aliases from usync_list_handlers.")]
    string[]? Handlers = null,
    [property: Description("Import every item even when uSync thinks it has not changed. Slower; only use when a normal import misses something.")]
    bool Force = false);

/// <summary>Lists the uSync handlers on this site, so the model can pick a group or handler.</summary>
[AITool("usync_list_handlers", "List uSync Handlers", ScopeId = uSyncReadScope.ScopeId)]
public class uSyncListHandlersTool : AIToolBase<uSyncListHandlersArgs>
{
    private readonly uSyncToolRunner _runner;

    public uSyncListHandlersTool(uSyncToolRunner runner) => _runner = runner;

    public override string Description =>
        "Lists the uSync handlers on this site with their alias and group. " +
        "Use it to find the group or handler aliases to pass to usync_report, usync_export or usync_import.";

    protected override Task<object> ExecuteAsync(uSyncListHandlersArgs args, CancellationToken cancellationToken = default)
        => Task.FromResult<object>(_runner.ListHandlers(HandlerActions.Report, args.Group));
}

/// <summary>Compares the uSync files on disk with the site, changing nothing.</summary>
[AITool("usync_report", "uSync Report", ScopeId = uSyncReadScope.ScopeId)]
public class uSyncReportTool : AIToolBase<uSyncRunArgs>
{
    private readonly uSyncToolRunner _runner;

    public uSyncReportTool(uSyncToolRunner runner) => _runner = runner;

    public override string Description =>
        "Compares the uSync files on disk with what is in this site and reports the differences. " +
        "Changes nothing. Run it before usync_import to see what an import would do.";

    protected override async Task<object> ExecuteAsync(uSyncRunArgs args, CancellationToken cancellationToken = default)
        => await _runner.RunAsync(new uSyncToolRunRequest(HandlerActions.Report, args.Group, args.Handlers), cancellationToken);
}

/// <summary>Writes the site's current state to the uSync folder.</summary>
[AITool("usync_export", "uSync Export", ScopeId = uSyncWriteScope.ScopeId, IsDestructive = true)]
public class uSyncExportTool : AIToolBase<uSyncRunArgs>
{
    private readonly uSyncToolRunner _runner;

    public uSyncExportTool(uSyncToolRunner runner) => _runner = runner;

    public override string Description =>
        "Exports this site's items to uSync files on disk, overwriting the files that are there. " +
        "The site itself is not changed.";

    protected override string? DescribeInvocation(uSyncRunArgs args)
        => $"Run a uSync export of {uSyncToolText.Target(args.Group, args.Handlers)}, overwriting the uSync files on disk.";

    protected override async Task<object> ExecuteAsync(uSyncRunArgs args, CancellationToken cancellationToken = default)
        => await _runner.RunAsync(new uSyncToolRunRequest(HandlerActions.Export, args.Group, args.Handlers), cancellationToken);
}

/// <summary>Applies the uSync files on disk to the site.</summary>
[AITool("usync_import", "uSync Import", ScopeId = uSyncWriteScope.ScopeId, IsDestructive = true)]
public class uSyncImportTool : AIToolBase<uSyncImportArgs>
{
    private readonly uSyncToolRunner _runner;

    public uSyncImportTool(uSyncToolRunner runner) => _runner = runner;

    public override string Description =>
        "Imports the uSync files on disk into this site, creating, updating and deleting items so the site matches the files. " +
        "This changes the site. Run usync_report first and tell the user what will change.";

    protected override string? DescribeInvocation(uSyncImportArgs args)
        => $"Run a uSync import of {uSyncToolText.Target(args.Group, args.Handlers)}{(args.Force ? " (forced)" : string.Empty)}. " +
           "This changes the site to match the uSync files on disk.";

    protected override async Task<object> ExecuteAsync(uSyncImportArgs args, CancellationToken cancellationToken = default)
        => await _runner.RunAsync(new uSyncToolRunRequest(HandlerActions.Import, args.Group, args.Handlers, args.Force), cancellationToken);
}

internal static class uSyncToolText
{
    /// <summary>What a run covers, in words, for the approval prompt.</summary>
    public static string Target(string? group, string[]? handlers)
    {
        if (handlers is { Length: > 0 }) return $"the {string.Join(", ", handlers)} handler{(handlers.Length == 1 ? string.Empty : "s")}";
        return string.IsNullOrWhiteSpace(group) ? "everything" : $"the {group} group";
    }
}

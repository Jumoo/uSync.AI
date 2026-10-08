using System.ComponentModel;
using Umbraco.AI.Core.Tools;
using Umbraco.AI.Core.Tools.Scopes;
using Umbraco.Cms.Core;
using uSync.AI.Complete.Security;
using uSync.AI.Complete.Services;
using uSync.Core.Sync;
using uSync.Expansions.Core.Restore;
using uSync.Expansions.Core.Restore.Strategies.Models;
using uSync.Publisher.Models;
using uSync.Publisher.Process.Models;
using uSync.Publisher.Strategies.Models;

namespace uSync.AI.Complete.Tools;

/// <summary>Read-only uSync.Complete tools.</summary>
[AIToolScope(ScopeId, Icon = "icon-infinity", Domain = "uSync")]
public class uSyncPublisherReadScope : AIToolScopeBase
{
    public const string ScopeId = "usync-publisher-read";
}

/// <summary>uSync.Complete tools that change this site or another server.</summary>
[AIToolScope(ScopeId, Icon = "icon-infinity", Domain = "uSync", IsDestructive = true)]
public class uSyncPublisherWriteScope : AIToolScopeBase
{
    public const string ScopeId = "usync-publisher";
}

/// <summary>
/// Pipeline and strategy aliases for uSync.Publisher. They are declared on an internal class
/// there (<c>PublisherStrategy</c>), so they are repeated here: keep them in step with it.
/// </summary>
internal static class uSyncPublisherPipelines
{
    public const string Pipeline = "PublisherPipeline";
    public const string RealtimePush = "RealtimePushStrategy";
    public const string RealtimePull = "RealtimePullStrategy";

    public static string StrategyFor(PublishMode mode) => mode == PublishMode.Pull ? RealtimePull : RealtimePush;
}

public record uSyncListServersArgs(
    [property: Description("Which servers to list: 'push' for servers content can be sent to, 'pull' for servers it can be fetched from. Defaults to 'push'.")]
    string? Mode = null);

public record uSyncPublishArgs(
    [property: Description("Alias of the server, from usync_list_servers.")]
    string Server,
    [property: Description("The key (GUID) of the content or media item.")]
    Guid ItemKey,
    [property: Description("'content' or 'media'. Defaults to 'content'.")]
    string? ItemType = null,
    [property: Description("Also include the item's descendants. Defaults to false: only the one item.")]
    bool IncludeChildren = false,
    [property: Description("Also include media the item uses. Defaults to true.")]
    bool IncludeMedia = true,
    [property: Description("Also include settings the item depends on, such as its document type and data types. Defaults to false.")]
    bool IncludeDependencies = false);

public record uSyncRestorePointArgs(
    [property: Description("Optional name for the restore point.")]
    string? Name = null,
    [property: Description("Include media files in the restore point. Defaults to false.")]
    bool IncludeMedia = false);

public sealed record uSyncServerList(bool Success, string? Message, IReadOnlyList<uSyncServerItem> Servers);

public sealed record uSyncServerItem(string Alias, string Name, string? Description);

/// <summary>Lists the publisher servers the acting user can use.</summary>
[AITool("usync_list_servers", "List uSync Servers", ScopeId = uSyncPublisherReadScope.ScopeId)]
public class uSyncListServersTool : AIToolBase<uSyncListServersArgs>
{
    private readonly IuSyncCompleteToolAuthorizer _authorizer;

    public uSyncListServersTool(IuSyncCompleteToolAuthorizer authorizer) => _authorizer = authorizer;

    public override string Description =>
        "Lists the uSync.Complete servers you can push content to or pull content from. " +
        "Use the alias with usync_publish_to_server or usync_pull_from_server.";

    protected override Task<object> ExecuteAsync(uSyncListServersArgs args, CancellationToken cancellationToken = default)
    {
        var mode = uSyncCompleteToolHelper.ParseMode(args.Mode);
        var (access, servers) = _authorizer.GetServers(mode);

        return Task.FromResult<object>(access.IsAuthorized
            ? new uSyncServerList(true, null, [.. servers.Select(x => new uSyncServerItem(x.Alias, x.Name, x.Description))])
            : new uSyncServerList(false, access.Message, []));
    }
}

/// <summary>Pushes a content or media item from this site to another server.</summary>
[AITool("usync_publish_to_server", "Publish to Server", ScopeId = uSyncPublisherWriteScope.ScopeId, IsDestructive = true)]
public class uSyncPublishToServerTool : AIToolBase<uSyncPublishArgs>
{
    private readonly IuSyncCompleteToolAuthorizer _authorizer;
    private readonly IuSyncCompletePipelineRunner _runner;

    public uSyncPublishToServerTool(IuSyncCompleteToolAuthorizer authorizer, IuSyncCompletePipelineRunner runner)
    {
        _authorizer = authorizer;
        _runner = runner;
    }

    public override string Description =>
        "Pushes a content or media item from this site to another server with uSync.Complete, making it live there. " +
        "This changes the other server. Use usync_list_servers for the server alias.";

    protected override string? DescribeInvocation(uSyncPublishArgs args)
        => $"Push {uSyncCompleteToolHelper.Describe(args)} to the '{args.Server}' server. This changes that server.";

    protected override Task<object> ExecuteAsync(uSyncPublishArgs args, CancellationToken cancellationToken = default)
        => uSyncCompleteToolHelper.PublishAsync(_authorizer, _runner, PublishMode.Push, args, cancellationToken);
}

/// <summary>Pulls a content or media item from another server into this site.</summary>
[AITool("usync_pull_from_server", "Pull from Server", ScopeId = uSyncPublisherWriteScope.ScopeId, IsDestructive = true)]
public class uSyncPullFromServerTool : AIToolBase<uSyncPublishArgs>
{
    private readonly IuSyncCompleteToolAuthorizer _authorizer;
    private readonly IuSyncCompletePipelineRunner _runner;

    public uSyncPullFromServerTool(IuSyncCompleteToolAuthorizer authorizer, IuSyncCompletePipelineRunner runner)
    {
        _authorizer = authorizer;
        _runner = runner;
    }

    public override string Description =>
        "Pulls a content or media item from another server into this site with uSync.Complete, overwriting this site's copy. " +
        "This changes this site. Use usync_list_servers with mode 'pull' for the server alias.";

    protected override string? DescribeInvocation(uSyncPublishArgs args)
        => $"Pull {uSyncCompleteToolHelper.Describe(args)} from the '{args.Server}' server. This overwrites it on this site.";

    protected override Task<object> ExecuteAsync(uSyncPublishArgs args, CancellationToken cancellationToken = default)
        => uSyncCompleteToolHelper.PublishAsync(_authorizer, _runner, PublishMode.Pull, args, cancellationToken);
}

/// <summary>Takes a uSync.Complete restore point of this site.</summary>
[AITool("usync_create_restore_point", "Create Restore Point", ScopeId = uSyncPublisherWriteScope.ScopeId, RequiresApproval = true)]
public class uSyncCreateRestorePointTool : AIToolBase<uSyncRestorePointArgs>
{
    private const string Operation = "restore point";

    private readonly IuSyncCompleteToolAuthorizer _authorizer;
    private readonly IuSyncCompletePipelineRunner _runner;

    public uSyncCreateRestorePointTool(IuSyncCompleteToolAuthorizer authorizer, IuSyncCompletePipelineRunner runner)
    {
        _authorizer = authorizer;
        _runner = runner;
    }

    public override string Description =>
        "Takes a uSync.Complete restore point: a snapshot of this site's settings and content that it can be rolled back to. " +
        "Changes nothing on the site. Worth doing before a large import or pull.";

    protected override string? DescribeInvocation(uSyncRestorePointArgs args)
        => $"Create a restore point{(string.IsNullOrWhiteSpace(args.Name) ? string.Empty : $" named '{args.Name}'")}" +
           $"{(args.IncludeMedia ? ", including media files" : string.Empty)}.";

    protected override async Task<object> ExecuteAsync(uSyncRestorePointArgs args, CancellationToken cancellationToken = default)
    {
        var access = _authorizer.AuthorizeRestorePoint();
        if (!access.IsAuthorized) return uSyncCompleteToolResult.Failed(Operation, access.Message!);

        var options = new RestorePointProcessingOptions
        {
            Source = "AI agent",
            Title = string.IsNullOrWhiteSpace(args.Name) ? $"AI agent {DateTime.Now:yyyy-MM-dd HH:mm:ss}" : args.Name,
            IncludeMedia = args.IncludeMedia,
            IsBackgroundRequest = true,
        };

        return await _runner.RunAsync(
            Operation, uSyncRestorePoints.Pipeline, uSyncRestorePoints.Strategies.Create, options, access.User!, null, cancellationToken);
    }
}

internal static class uSyncCompleteToolHelper
{
    public static PublishMode ParseMode(string? mode)
        => string.Equals(mode?.Trim(), "pull", StringComparison.OrdinalIgnoreCase) ? PublishMode.Pull : PublishMode.Push;

    public static bool IsMedia(string? itemType) => string.Equals(itemType?.Trim(), "media", StringComparison.OrdinalIgnoreCase);

    /// <summary>What a publish covers, in words, for the approval prompt.</summary>
    public static string Describe(uSyncPublishArgs args)
    {
        var extras = new List<string>();
        if (args.IncludeChildren) extras.Add("its descendants");
        if (args.IncludeMedia) extras.Add("the media it uses");
        if (args.IncludeDependencies) extras.Add("the settings it depends on");

        var what = $"the {(IsMedia(args.ItemType) ? "media" : "content")} item {args.ItemKey}";
        return extras.Count == 0 ? what : $"{what}, with {string.Join(", ", extras)},";
    }

    public static async Task<object> PublishAsync(
        IuSyncCompleteToolAuthorizer authorizer, IuSyncCompletePipelineRunner runner,
        PublishMode mode, uSyncPublishArgs args, CancellationToken cancellationToken)
    {
        var operation = mode == PublishMode.Pull ? "pull" : "push";

        // a publish with no item is a publish of the whole tree, which is never what a model
        // that forgot the key meant.
        if (args.ItemKey == Guid.Empty)
            return uSyncCompleteToolResult.Failed(operation, "An item key is needed. This tool sends one item, not the whole site.", args.Server);

        var authorization = authorizer.AuthorizePublish(mode, args.Server);
        if (!authorization.IsAuthorized)
            return uSyncCompleteToolResult.Failed(operation, authorization.Message!, args.Server);

        var server = authorization.Server!;
        var entityType = IsMedia(args.ItemType) ? Constants.UdiEntityType.Media : Constants.UdiEntityType.Document;

        var options = new PublisherProcessingOptions
        {
            Mode = mode,
            Server = server.Alias,
            EntityType = entityType,
            IsBackgroundRequest = true,
            Items =
            [
                new SyncItem
                {
                    Name = "AI agent item",
                    Udi = new GuidUdi(entityType, args.ItemKey),
                    Icon = "icon-document",
                },
            ],
            // the pipeline derives every item's dependency flags from this object
            PublisherOptions = new SyncPublisherOptions
            {
                IncludeChildren = args.IncludeChildren,
                IncludeMedia = args.IncludeMedia,
                IncludeAncestors = true,
                IncludeDependencies = args.IncludeDependencies,
                IncludeFileHash = true,
            },
        };

        return await runner.RunAsync(
            operation, uSyncPublisherPipelines.Pipeline, uSyncPublisherPipelines.StrategyFor(mode), options,
            authorization.User!, server.Alias, cancellationToken);
    }
}

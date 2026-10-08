using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Extensions;
using uSync.AI.Tools.Security;
using uSync.Publisher.Configuration;
using uSync.Publisher.Models;

namespace uSync.AI.Complete.Security;

/// <summary>The publisher servers a tool can see. A seam over uSync.Publisher's own service.</summary>
public interface IuSyncCompleteServerSource
{
    IEnumerable<SyncPublishServer> GetServers();
}

/// <inheritdoc cref="IuSyncCompleteServerSource"/>
public sealed class uSyncCompleteServerSource(SyncPublishConfigService configService) : IuSyncCompleteServerSource
{
    public IEnumerable<SyncPublishServer> GetServers() => configService.GetServers();
}

/// <summary>The outcome of authorizing a publisher tool against one server.</summary>
public sealed record uSyncCompleteAuthorization(bool IsAuthorized, IUser? User, SyncPublishServer? Server, string? Message)
{
    public static uSyncCompleteAuthorization Denied(string message, IUser? user = null) => new(false, user, null, message);
}

/// <summary>
/// Authorizes a uSync.Complete agent tool against the backoffice user the agent is acting for.
/// </summary>
/// <remarks>
/// Layered on <see cref="IuSyncToolAuthorizer"/>, so the user must first be allowed to use uSync
/// at all. On top of that it applies the same three rules as uSync.Publisher's own backoffice:
/// the user's groups hold the push or pull permission, the server has that direction enabled,
/// and the server is open to one of the user's groups. The publisher checks the permission
/// again when the pipeline is created; it is checked here first so a refusal comes back as a
/// clear message and nothing has been started.
/// </remarks>
public interface IuSyncCompleteToolAuthorizer
{
    /// <summary>May the acting user push to, or pull from, this server?</summary>
    uSyncCompleteAuthorization AuthorizePublish(PublishMode mode, string? serverAlias);

    /// <summary>The servers the acting user could push to or pull from. Empty when they can't at all.</summary>
    (uSyncToolAuthorization Access, IReadOnlyList<SyncPublishServer> Servers) GetServers(PublishMode mode);

    /// <summary>May the acting user take a restore point?</summary>
    uSyncToolAuthorization AuthorizeRestorePoint();
}

/// <inheritdoc cref="IuSyncCompleteToolAuthorizer"/>
public sealed class uSyncCompleteToolAuthorizer : IuSyncCompleteToolAuthorizer
{
    // uSyncPublisher.Permissions is internal to uSync.Publisher; these are its values.
    public const string PushPermission = "uSync.UserPermission.Push";
    public const string PullPermission = "uSync.UserPermission.Pull";

    private readonly IuSyncToolAuthorizer _toolAuthorizer;
    private readonly IuSyncCompleteServerSource _servers;

    public uSyncCompleteToolAuthorizer(IuSyncToolAuthorizer toolAuthorizer, IuSyncCompleteServerSource servers)
    {
        _toolAuthorizer = toolAuthorizer;
        _servers = servers;
    }

    public uSyncCompleteAuthorization AuthorizePublish(PublishMode mode, string? serverAlias)
    {
        var (access, servers) = GetServers(mode);
        if (!access.IsAuthorized) return uSyncCompleteAuthorization.Denied(access.Message!, access.User);

        // one answer for "no such server" and "not one of yours", so a tool can't be used to
        // find out which servers exist.
        var server = servers.FirstOrDefault(x => string.Equals(x.Alias, serverAlias, StringComparison.OrdinalIgnoreCase));
        return server is null
            ? uSyncCompleteAuthorization.Denied($"There is no server '{serverAlias}' you can {Verb(mode)}. Use usync_list_servers to see the ones you can.", access.User)
            : new uSyncCompleteAuthorization(true, access.User, server, null);
    }

    public (uSyncToolAuthorization Access, IReadOnlyList<SyncPublishServer> Servers) GetServers(PublishMode mode)
    {
        // a pull changes this site, the same as an import; a push only reads it.
        var access = _toolAuthorizer.Authorize(mode == PublishMode.Pull ? uSyncToolAccess.Import : uSyncToolAccess.Export);
        if (!access.IsAuthorized) return (access, []);

        var user = access.User!;
        var permission = mode == PublishMode.Pull ? PullPermission : PushPermission;

        if (!user.Groups.Any(group => group.Permissions.Contains(permission)))
        {
            return (uSyncToolAuthorization.Denied(
                $"You do not have uSync.Complete's permission to {(mode == PublishMode.Pull ? "pull" : "push")}.", user), []);
        }

        var groupKeys = user.Groups.Select(x => x.Key).ToArray();

        var servers = _servers.GetServers()
            .Where(x => mode == PublishMode.Pull ? x.PullEnabled : x.PushEnabled)
            .Where(x => x.SendSettings?.UserGroups is not { Length: > 0 } allowed || allowed.ContainsAny(groupKeys))
            .ToList();

        return (access, servers);
    }

    public uSyncToolAuthorization AuthorizeRestorePoint() => _toolAuthorizer.Authorize(uSyncToolAccess.Export);

    private static string Verb(PublishMode mode) => mode == PublishMode.Pull ? "pull from" : "push to";
}

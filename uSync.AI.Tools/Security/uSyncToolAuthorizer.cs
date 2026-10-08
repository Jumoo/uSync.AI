using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Extensions;
using uSync.BackOffice;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace uSync.AI.Tools.Security;

/// <summary>What a uSync tool is about to do, from least to most access needed.</summary>
public enum uSyncToolAccess
{
    /// <summary>Look at uSync's state without changing anything (report, list).</summary>
    Read,

    /// <summary>Write uSync files to disk (export).</summary>
    Export,

    /// <summary>Change the site from uSync files (import).</summary>
    Import,
}

/// <summary>The outcome of an authorization check.</summary>
/// <param name="IsAuthorized">Whether the tool may run.</param>
/// <param name="User">The acting user, when there is one.</param>
/// <param name="Message">Why it was refused. Shown to the model, so it holds no sensitive detail.</param>
public sealed record uSyncToolAuthorization(bool IsAuthorized, IUser? User, string? Message)
{
    public static uSyncToolAuthorization Denied(string message, IUser? user = null) => new(false, user, message);

    public static uSyncToolAuthorization Allowed(IUser user) => new(true, user, null);
}

/// <summary>
/// Authorizes a uSync agent tool against the backoffice user the agent is acting for.
/// </summary>
/// <remarks>
/// uSync's services do not authorize their callers - its management API controllers do, with the
/// <c>TreeAccessuSync</c> policy. A tool calls those services directly, so every tool has to
/// call this first or an agent could run uSync for a user who can't open the uSync dashboard.
/// Which tools an agent offers is configuration anyone who can edit the agent can change; this
/// check is the one that can't be configured away.
/// </remarks>
public interface IuSyncToolAuthorizer
{
    uSyncToolAuthorization Authorize(uSyncToolAccess access);
}

/// <inheritdoc cref="IuSyncToolAuthorizer"/>
public sealed class uSyncToolAuthorizer : IuSyncToolAuthorizer
{
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IOptionsMonitor<uSyncAIToolsOptions> _options;

    public uSyncToolAuthorizer(IBackOfficeSecurityAccessor backOfficeSecurityAccessor, IOptionsMonitor<uSyncAIToolsOptions> options)
    {
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _options = options;
    }

    public uSyncToolAuthorization Authorize(uSyncToolAccess access)
    {
        // No user means the agent is running in the background (a schedule, an automation).
        // There is nobody whose permissions it could be acting with, so it gets none.
        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
            return uSyncToolAuthorization.Denied("uSync tools need a signed-in backoffice user and there isn't one for this request.");

        // the same rule as uSync's own TreeAccessuSync policy
        if (!user.AllowedSections.ContainsAny([UmbracoConstants.Applications.Settings, uSyncConstants.uSyncSection]))
            return uSyncToolAuthorization.Denied("You do not have access to uSync. It needs access to the Settings section.", user);

        if (access == uSyncToolAccess.Import && _options.CurrentValue.RequireAdminForImport && !user.IsAdmin())
            return uSyncToolAuthorization.Denied("Only administrators can run a uSync import from an AI agent.", user);

        return uSyncToolAuthorization.Allowed(user);
    }
}

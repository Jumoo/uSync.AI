using System.Runtime.CompilerServices;
using Umbraco.AI.Agent.Core.Agents;
using Umbraco.Cms.Core.Services;

namespace uSync.AI.Agent.Services;

/// <summary>
/// Wraps Umbraco.AI.Agent's public service layer for the agent handler and serializer, plus the
/// user group lookups an agent's per-group tool permissions need.
/// </summary>
public sealed class SyncAIAgentService
{
    private readonly IAIAgentService _agents;
    private readonly IUserGroupService _userGroups;

    public SyncAIAgentService(IAIAgentService agents, IUserGroupService userGroups)
    {
        _agents = agents;
        _userGroups = userGroups;
    }

    public Task<AIAgent?> GetAgentAsync(Guid key) => _agents.GetAgentAsync(key);

    public Task<AIAgent?> GetAgentAsync(string alias) => _agents.GetAgentByAliasAsync(alias);

    public Task<IEnumerable<AIAgent>> GetAgentsAsync() => _agents.GetAgentsAsync();

    public Task SaveAgentAsync(AIAgent item) => _agents.SaveAgentAsync(item);

    public Task DeleteAgentAsync(AIAgent item) => _agents.DeleteAgentAsync(item.Id);

    public async Task<string?> GetUserGroupAliasAsync(Guid key) => (await _userGroups.GetAsync(key))?.Alias;

    /// <summary>
    /// A user group's key on this server. User groups are created per site, so the same group
    /// usually has a different key on each: the alias is tried first, then the key from the file.
    /// </summary>
    public async Task<Guid?> ResolveUserGroupAsync(Guid key, string? alias)
    {
        if (!string.IsNullOrWhiteSpace(alias) && await _userGroups.GetAsync(alias) is { } byAlias) return byAlias.Key;
        if (key != Guid.Empty && await _userGroups.GetAsync(key) is { } byKey) return byKey.Key;
        return null;
    }
}

/// <summary>
/// Sets the Id on a new <see cref="AIAgent"/> so it keeps the Guid it had on the source server.
/// See <c>AIEntityKeys</c> in uSync.AI.Sync for why this goes through an unsafe accessor.
/// </summary>
public static class AIAgentKeys
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Id")]
    private static extern void SetIdCore(AIAgent entity, Guid id);

    public static AIAgent WithId(this AIAgent entity, Guid id) { SetIdCore(entity, id); return entity; }
}

using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Profiles;
using Umbraco.AI.Core.Providers;
using Umbraco.AI.Core.Settings;

namespace uSync.AI.Sync.Services;

/// <summary>
/// The only thing uSync.AI's handlers and serializers talk to. Wraps Umbraco.AI's public service
/// layer (its repositories are internal) and holds the alias/key lookups the serializers share.
/// </summary>
public sealed class SyncAIService
{
    private readonly IAIConnectionService _connections;
    private readonly IAIGuardrailService _guardrails;
    private readonly IAIContextService _contexts;
    private readonly IAIProfileService _profiles;
    private readonly IAISettingsService _settings;
    private readonly AIProviderCollection _providers;

    public SyncAIService(
        IAIConnectionService connections,
        IAIGuardrailService guardrails,
        IAIContextService contexts,
        IAIProfileService profiles,
        IAISettingsService settings,
        AIProviderCollection providers)
    {
        _connections = connections;
        _guardrails = guardrails;
        _contexts = contexts;
        _profiles = profiles;
        _settings = settings;
        _providers = providers;
    }

    // connections

    public Task<AIConnection?> GetConnectionAsync(Guid key) => _connections.GetConnectionAsync(key);

    public Task<AIConnection?> GetConnectionAsync(string alias) => _connections.GetConnectionByAliasAsync(alias);

    public Task<IEnumerable<AIConnection>> GetConnectionsAsync() => _connections.GetConnectionsAsync();

    public Task SaveConnectionAsync(AIConnection item) => _connections.SaveConnectionAsync(item);

    public Task DeleteConnectionAsync(AIConnection item) => _connections.DeleteConnectionAsync(item.Id);

    /// <summary>
    /// The settings type of a connection's provider - it is what says which settings are
    /// sensitive. Null when the provider isn't installed on this server.
    /// </summary>
    public Type? GetConnectionSettingsType(string providerId) => _providers.GetById(providerId)?.SettingsType;

    // guardrails

    public Task<AIGuardrail?> GetGuardrailAsync(Guid key) => _guardrails.GetGuardrailAsync(key);

    public Task<AIGuardrail?> GetGuardrailAsync(string alias) => _guardrails.GetGuardrailByAliasAsync(alias);

    public Task<IEnumerable<AIGuardrail>> GetGuardrailsAsync() => _guardrails.GetGuardrailsAsync();

    public Task SaveGuardrailAsync(AIGuardrail item) => _guardrails.SaveGuardrailAsync(item);

    public Task DeleteGuardrailAsync(AIGuardrail item) => _guardrails.DeleteGuardrailAsync(item.Id);

    // contexts

    public Task<AIContext?> GetContextAsync(Guid key) => _contexts.GetContextAsync(key);

    public Task<AIContext?> GetContextAsync(string alias) => _contexts.GetContextByAliasAsync(alias);

    public Task<IEnumerable<AIContext>> GetContextsAsync() => _contexts.GetContextsAsync();

    public Task SaveContextAsync(AIContext item) => _contexts.SaveContextAsync(item);

    public Task DeleteContextAsync(AIContext item) => _contexts.DeleteContextAsync(item.Id);

    // profiles

    public Task<AIProfile?> GetProfileAsync(Guid key) => _profiles.GetProfileAsync(key);

    public Task<AIProfile?> GetProfileAsync(string alias) => _profiles.GetProfileByAliasAsync(alias);

    public Task<IEnumerable<AIProfile>> GetProfilesAsync() => _profiles.GetAllProfilesAsync();

    public Task SaveProfileAsync(AIProfile item) => _profiles.SaveProfileAsync(item);

    public Task DeleteProfileAsync(AIProfile item) => _profiles.DeleteProfileAsync(item.Id);

    // settings

    public Task<AISettings> GetSettingsAsync() => _settings.GetSettingsAsync();

    public Task SaveSettingsAsync(AISettings item) => _settings.SaveSettingsAsync(item);

    // references - every reference is written as a Key plus the portable alias, and read back
    // key first, so a file still resolves on a server where the same item was created by hand.

    public async Task<string?> GetConnectionAliasAsync(Guid key) => (await GetConnectionAsync(key))?.Alias;

    public async Task<string?> GetProfileAliasAsync(Guid key) => (await GetProfileAsync(key))?.Alias;

    public async Task<string?> GetGuardrailAliasAsync(Guid key) => (await GetGuardrailAsync(key))?.Alias;

    public async Task<string?> GetContextAliasAsync(Guid key) => (await GetContextAsync(key))?.Alias;

    public async Task<Guid?> ResolveConnectionAsync(Guid key, string? alias)
        => (await FindAsync(key, alias, GetConnectionAsync, GetConnectionAsync))?.Id;

    public async Task<Guid?> ResolveProfileAsync(Guid key, string? alias)
        => (await FindAsync(key, alias, GetProfileAsync, GetProfileAsync))?.Id;

    public async Task<Guid?> ResolveGuardrailAsync(Guid key, string? alias)
        => (await FindAsync(key, alias, GetGuardrailAsync, GetGuardrailAsync))?.Id;

    public async Task<Guid?> ResolveContextAsync(Guid key, string? alias)
        => (await FindAsync(key, alias, GetContextAsync, GetContextAsync))?.Id;

    private static async Task<T?> FindAsync<T>(Guid key, string? alias, Func<Guid, Task<T?>> byKey, Func<string, Task<T?>> byAlias)
        where T : class
    {
        if (key != Guid.Empty && await byKey(key) is { } found) return found;
        return string.IsNullOrWhiteSpace(alias) ? null : await byAlias(alias);
    }
}

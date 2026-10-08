using System.Runtime.CompilerServices;
using Umbraco.AI.Prompt.Core.Prompts;

namespace uSync.AI.Prompt.Services;

/// <summary>
/// Wraps Umbraco.AI.Prompt's public service layer for the prompt handler and serializer.
/// </summary>
public sealed class SyncAIPromptService
{
    private readonly IAIPromptService _prompts;

    public SyncAIPromptService(IAIPromptService prompts)
    {
        _prompts = prompts;
    }

    public Task<AIPrompt?> GetPromptAsync(Guid key) => _prompts.GetPromptAsync(key);

    public Task<AIPrompt?> GetPromptAsync(string alias) => _prompts.GetPromptByAliasAsync(alias);

    public Task<IEnumerable<AIPrompt>> GetPromptsAsync() => _prompts.GetPromptsAsync();

    public Task SavePromptAsync(AIPrompt item) => _prompts.SavePromptAsync(item);

    public Task DeletePromptAsync(AIPrompt item) => _prompts.DeletePromptAsync(item.Id);
}

/// <summary>
/// Sets the Id on a new <see cref="AIPrompt"/> so it keeps the Guid it had on the source server.
/// See <c>AIEntityKeys</c> in uSync.AI.Sync for why this goes through an unsafe accessor.
/// </summary>
public static class AIPromptKeys
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Id")]
    private static extern void SetIdCore(AIPrompt entity, Guid id);

    public static AIPrompt WithId(this AIPrompt entity, Guid id) { SetIdCore(entity, id); return entity; }
}

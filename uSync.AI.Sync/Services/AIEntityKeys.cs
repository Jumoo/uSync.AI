using System.Runtime.CompilerServices;
using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Profiles;

namespace uSync.AI.Sync.Services;

/// <summary>
/// Sets the Id on a new Umbraco.AI entity so it keeps the Guid it had on the source server.
/// </summary>
/// <remarks>
/// Every AI entity declares <c>Id { get; internal set; }</c> and the save services assign a new
/// Guid when it is empty. Umbraco.AI.Deploy can set it because it is a friend assembly; this
/// package is not, so it goes through <see cref="UnsafeAccessorAttribute"/>, which binds to the
/// setter at JIT time (no reflection per call) and fails loudly if the setter is ever removed.
/// Ids have to survive a sync because prompts, profiles, agent config and content property
/// values all refer to contexts, guardrails and profiles by Guid. See UPSTREAM-REQUESTS.md.
/// </remarks>
public static class AIEntityKeys
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Id")]
    private static extern void SetIdCore(AIConnection entity, Guid id);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Id")]
    private static extern void SetIdCore(AIGuardrail entity, Guid id);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Id")]
    private static extern void SetIdCore(AIGuardrailRule entity, Guid id);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Id")]
    private static extern void SetIdCore(AIContext entity, Guid id);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Id")]
    private static extern void SetIdCore(AIContextResource entity, Guid id);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Id")]
    private static extern void SetIdCore(AIProfile entity, Guid id);

    public static AIConnection WithId(this AIConnection entity, Guid id) { SetIdCore(entity, id); return entity; }

    public static AIGuardrail WithId(this AIGuardrail entity, Guid id) { SetIdCore(entity, id); return entity; }

    public static AIGuardrailRule WithId(this AIGuardrailRule entity, Guid id) { SetIdCore(entity, id); return entity; }

    public static AIContext WithId(this AIContext entity, Guid id) { SetIdCore(entity, id); return entity; }

    public static AIContextResource WithId(this AIContextResource entity, Guid id) { SetIdCore(entity, id); return entity; }

    public static AIProfile WithId(this AIProfile entity, Guid id) { SetIdCore(entity, id); return entity; }
}

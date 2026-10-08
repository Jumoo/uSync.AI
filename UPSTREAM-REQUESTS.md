# Upstream API requests

Things discovered while building the uSync.AI packages that would be better served by a small
change in Umbraco.AI or uSync itself than by a workaround here. Ranked roughly by cost to us.

## Umbraco.AI

1. **Entity `Id` setters are `internal`.**
   `AIConnection`, `AIProfile`, `AIContext`, `AIGuardrail`, `AIPrompt` and `AIAgent` all declare
   `public Guid Id { get; internal set; }`, and the `Save*Async` methods assign `Guid.NewGuid()`
   when the Id is empty. `Umbraco.AI.Deploy` creates entities with the source environment's Id
   because it is a friend assembly (`InternalsVisibleTo`); a third-party sync package cannot.
   Ids have to survive a sync: prompts, profiles, agent config and content property values all
   reference contexts, guardrails and profiles by Guid.
   Workaround: `AIEntityKeys` in `uSync.AI.Sync` sets the Id through an `[UnsafeAccessor]`.
   Fix: a public `init` setter, or a `Create*Async(entity, Guid id)` overload. Non-breaking.

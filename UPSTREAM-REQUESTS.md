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

2. **Connection settings reach notification handlers decrypted, so "ignore encrypted" filters
   never match.** `AIConnectionSavedNotification.Entity.Settings` (and what
   `IAIConnectionService.GetConnectionAsync` returns) holds sensitive fields in plain text; the
   `ENC:` prefix only exists in storage. Seen on a 17.5.2 test site: saving an OpenAI connection
   through the management API handed our handler `apiKey` as typed.
   `uSync.AI.Sync` therefore decides what is secret from `[AIField(IsSensitive = true)]` on the
   provider's settings type, not from the value.
   Worth raising: `Umbraco.AI.Deploy`'s `FilterSensitiveSettings` builds its artifact from the
   same entity and its default (`IgnoreEncrypted = true`, `IgnoreSensitive = false`) only tests
   for the `ENC:` prefix. We have not run Deploy to confirm, but on reading the code its default
   would write API keys into `.uda` files.

3. **`AIEntityDeletedNotification<T>` carries only `EntityId`.** uSync needs the entity (its
   alias names the file) to write a delete marker. Workaround: handlers also listen to the
   `Deleting` notification, load the entity while it still exists and hold it until `Deleted`
   (`SyncAIPendingDeletes`). Fix: expose the deleted entity on the notification.

4. **`AIProfileSettingsSerializer` is internal.** Its capability switch is duplicated in
   `AIProfileSerializer.DeserializeSettings`, and will need updating by hand when a capability
   is added. Fix: make it public.

## uSync.Complete

1. **The publisher assumes a backoffice entity type is a valid UDI entity type.** Umbraco.AI's
   are not: `uai:agent` has a colon, and `umb://uai:agent/{id}` does not parse. Three places
   build or check a UDI from the entity type the backoffice hands them:
   - the `usync.publisher.push.condition` on the `publisher-push` / `publisher-pull` kinds
     builds `umb://{entityType}/{unique}`;
   - `PublisherServerController.GetAvailableServers` returns no servers when that UDI doesn't
     parse;
   - the client's `servers.source.ts` caches the answer by entity type, not by UDI, so one
     failed lookup leaves the push dialog with an empty server list.
   `SyncItemManagerCollection.GetSyncEntityAsync` has the same problem for tree roots: with no
   id it calls `Udi.Create(entityType)`.
   Workaround: `uSync.AI.Complete` ships its own push and pull entity actions
   (`complete-client/src/actions/ai-publish.action.ts`). They ask the item manager for the item
   first and open the publisher's dialog with the entity type from the UDI that comes back.
   Roots are not supported.
   Fix: let an item manager declare the backoffice entity types it maps, and have
   `uSyncEntityTypeHelper.ConvertClientEntityType` consult the item managers before the UDI is
   built. `uSync.AI.Complete` could then use the stock kinds and drop its own actions.

2. **The publisher's client can't be imported.** `@jumoo/usync-publisher-assets` gives types,
   but the process modal token and `PublisherStrategyContext` aren't reachable at run time, so
   the workaround above rebuilds the modal token from its alias string. An exported token
   would stop that drifting.

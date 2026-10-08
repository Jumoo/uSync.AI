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
   Raised as [umbraco/Umbraco.AI#544](https://github.com/umbraco/Umbraco.AI/issues/544), with a
   public `init` fix (also covering `AIContextResource` and `AIGuardrailRule`) in
   [umbraco/Umbraco.AI#545](https://github.com/umbraco/Umbraco.AI/pull/545) against `v18/dev`.
   The workaround stays until a fix ships on the v17 line too.

2. **Connection settings reach notification handlers decrypted, so "ignore encrypted" filters
   never match.** `AIConnectionSavedNotification.Entity.Settings` (and what
   `IAIConnectionService.GetConnectionAsync` returns) holds sensitive fields in plain text; the
   `ENC:` prefix only exists in storage. Seen on a 17.5.2 test site: saving an OpenAI connection
   through the management API handed our handler `apiKey` as typed.
   `uSync.AI.Sync` therefore decides what is secret from `[AIField(IsSensitive = true)]` on the
   provider's settings type, not from the value, and leaves secrets out by default
   (`IgnoreSecretValues`, which can be turned off to sync them).
   Worth asking about: `Umbraco.AI.Deploy`'s `FilterSensitiveSettings` works from the same
   entity, and its `IgnoreEncrypted` option only tests for the `ENC:` prefix, so as far as we
   can tell from the code it doesn't filter anything in practice and API keys travel in `.uda`
   files. That may well be intended - Deploy moves secrets between environments the site owner
   controls - but if so the option name suggests otherwise, and the two packages should agree
   on what the default is.

3. **`AIEntityDeletedNotification<T>` carries only `EntityId`.** uSync needs the entity (its
   alias names the file) to write a delete marker. Workaround: handlers also listen to the
   `Deleting` notification, load the entity while it still exists and hold it until `Deleted`
   (`SyncAIPendingDeletes`). Fix: expose the deleted entity on the notification.

4. **`AIProfileSettingsSerializer` is internal.** Its capability switch is duplicated in
   `AIProfileSerializer.DeserializeSettings`, and will need updating by hand when a capability
   is added. Fix: make it public.

5. **The management API accepts aliases the backoffice won't save.** The backoffice's alias
   field requires `^[a-z0-9\-]+$`, but `POST /connections` (and profiles, prompts, agents)
   accepted `openAiTest`. Seen on a 17.5.2 test site: once an item has such an alias, Save in
   its workspace does nothing and shows no error - the locked alias input fails validation and
   throws (`setValidity` called with an empty message) instead of showing its pattern message.
   It matters to uSync because an import takes the alias from the file as is, so an item
   created through the API on one site imports cleanly onto another and then can't be edited
   there. Workaround: none in uSync.AI; rename the alias through the API. Fix: validate the
   alias the same way on the server, and have the field show its message.

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

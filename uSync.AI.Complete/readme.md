# uSync.AI.Complete

[uSync.Complete](https://jumoo.co.uk/usync/complete/) support for
[Umbraco.AI](https://docs.umbraco.com/ai-in-umbraco): push and pull AI items between servers.

Most sites want the `uSync.Complete.AI` package, which installs this alongside `uSync.AI`.

## Push and pull

Connections, guardrails, contexts, profiles, prompts and agents each get **Push to server…**
and **Pull from server…** in their actions menu, for users who have uSync.Complete's push or
pull permission.

With "Include dependencies" on, an item brings what it needs with it. Pushing an agent also
sends its profile, that profile's connection, and the contexts and guardrails either of them
use. Items arrive on the other server with the same Ids.

A connection's API key is never sent. On a server that doesn't have the connection yet it
arrives with a placeholder key and a warning; enter the real key there once. See the
`uSync.AI.Sync` readme.

## Good to know

- There is no push or pull on the tree roots (for example "Agents"). Use the uSync dashboard,
  or a uSync.Complete sync of the AI group, to move everything at once.
- An agent's per-user-group tool permissions follow the user group's alias, so they apply to
  the matching group on the other server.

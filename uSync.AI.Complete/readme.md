# uSync.AI.Complete

[uSync.Complete](https://jumoo.co.uk/usync/complete/) support for
[Umbraco.AI](https://docs.umbraco.com/ai-in-umbraco): push and pull AI items between servers,
and give AI agents tools that drive the publisher.

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

## Agent tools

| Tool | Scope | What it does |
| --- | --- | --- |
| `usync_list_servers` | `usync-publisher-read` | Lists the servers you can push to or pull from |
| `usync_publish_to_server` | `usync-publisher` | Pushes one content or media item to another server |
| `usync_pull_from_server` | `usync-publisher` | Pulls one content or media item into this site |
| `usync_create_restore_point` | `usync-publisher` | Takes a restore point of this site |

Push and pull send a single item by key, optionally with its descendants, the media it uses
and the settings it depends on. They never send the whole tree.

A tool is only available to an agent that has been given it, or its scope. The three that
change something ask the user to approve each call, and the prompt names the server and item.

### Who can run them

Each tool checks the backoffice user the agent is acting for. The agent's settings can't
override this.

| | Needs |
| --- | --- |
| Any tool | Access to uSync (the Settings section), as for the `uSync.AI.Tools` tools |
| Push | uSync.Complete's push permission |
| Pull | uSync.Complete's pull permission, and an administrator (a pull changes this site, like an import) |
| A server | Push or pull enabled on that server, and the server open to one of the user's groups |
| No signed-in user | Refused |

The publish itself runs as that user, so uSync.Complete applies its own permission checks too.
`RequireAdminForImport` in the `uSync.AI.Tools` options also controls the administrator rule
for pull.

## Good to know

- There is no push or pull on the tree roots (for example "Agents"). Use the uSync dashboard,
  or a uSync.Complete sync of the AI group, to move everything at once.
- An agent's per-user-group tool permissions follow the user group's alias, so they apply to
  the matching group on the other server.

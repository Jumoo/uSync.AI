# uSync.AI.Agent

uSync handler and serializer for [Umbraco.AI.Agent](https://docs.umbraco.com/ai-in-umbraco/add-ons/agent)
agents. An agent is written to the `AI-Agents` uSync folder when it is saved, and travels
between servers with the rest of your uSync files.

Most sites want the `uSync.AI` package, which installs this alongside settings and prompt sync.

## What is synced

Everything on the agent: name, description, type, surfaces, scope rules, its profile and
guardrails, and its config. For a standard agent that is the instructions, contexts, allowed
tools and tool scopes, output schema and per-user-group tool permissions. For an orchestrated
agent it is the workflow and its settings.

An agent keeps the same Id on every server. Agents import last, after everything they refer to.

## References that are missing on the target

A profile, guardrail or context is recorded by Id and alias. If it isn't on the target server
the agent is still imported, with that reference left off and a warning; the next import
restores it once the item exists.

User groups are matched by alias, because the same group has a different key on each site. If
a group doesn't exist on the target, its tool permissions are not applied there and the import
warns. The agent then uses its default tools for that group; it never gains any.

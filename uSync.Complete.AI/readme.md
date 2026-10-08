# uSync.Complete.AI

uSync.Complete for [Umbraco.AI](https://docs.umbraco.com/ai-in-umbraco). Install this one
package to get everything in `uSync.AI`, plus push and pull of AI items between servers and
agent tools for the uSync.Complete publisher.

```
dotnet add package uSync.Complete.AI
```

It installs:

| Package | What it does |
| --- | --- |
| `uSync.AI` | Syncs AI connections, guardrails, contexts, profiles, settings, prompts and agents, and adds agent tools for uSync report, export and import |
| `uSync.AI.Complete` | Push and pull AI items between servers, and agent tools to publish content, pull content and take restore points |

## What you need

uSync.Complete (and a licence for it), Umbraco.AI, Umbraco.AI.Prompt and Umbraco.AI.Agent.
Without uSync.Complete, install `uSync.AI` instead.

See the `uSync.AI.Complete` readme for the push and pull menus, the agent tools and who can
run them.

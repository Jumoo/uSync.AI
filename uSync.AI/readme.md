# uSync.AI

uSync for [Umbraco.AI](https://docs.umbraco.com/ai-in-umbraco). Install this one package to
sync your AI setup between environments and let AI agents run uSync.

```
dotnet add package uSync.AI
```

It installs four packages:

| Package | What it does |
| --- | --- |
| `uSync.AI.Sync` | Syncs connections, guardrails, contexts, profiles and AI settings |
| `uSync.AI.Prompt` | Syncs prompts |
| `uSync.AI.Agent` | Syncs agents |
| `uSync.AI.Tools` | Agent tools for uSync report, export and import |

## What you need

uSync, Umbraco.AI, Umbraco.AI.Prompt and Umbraco.AI.Agent. Installing `uSync.AI` brings the
prompt and agent add-ons with it. If you don't use one of them, install the individual
packages above instead.

For uSync.Complete (push and pull AI items between servers, and publisher tools for agents)
install `uSync.Complete.AI`, which includes everything here.

## Good to know

- AI items appear in the uSync dashboard under their own **AI** group and are written to
  `AI-*` folders in your uSync folder.
- API keys are never written to disk. A connection synced to a new server arrives without its
  key; enter it there once. See the `uSync.AI.Sync` readme for the details and options.
- The agent tools do nothing until you give them to an agent, and each one checks that the
  user the agent is acting for has access to uSync. See the `uSync.AI.Tools` readme.

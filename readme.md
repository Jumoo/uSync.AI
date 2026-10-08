# uSync.AI

uSync and uSync.Complete support for [Umbraco.AI](https://docs.umbraco.com/ai-in-umbraco).
Sync AI connections, profiles, contexts, guardrails, settings, prompts and agents between
environments as files, push and pull them with uSync.Complete, and let AI agents run uSync.

## Packages

| Package | What it does | Needs |
| --- | --- | --- |
| `uSync.AI` | Meta package: installs the four packages below | uSync, Umbraco.AI, Umbraco.AI.Prompt, Umbraco.AI.Agent |
| `uSync.AI.Sync` | Handlers and serializers for connections, profiles, contexts, guardrails and settings | uSync, Umbraco.AI |
| `uSync.AI.Prompt` | Handler and serializer for prompts | Umbraco.AI.Prompt |
| `uSync.AI.Agent` | Handler and serializer for agents | Umbraco.AI.Agent |
| `uSync.AI.Tools` | Agent tools: uSync report, export and import, checked against the user's uSync access | uSync, Umbraco.AI |
| `uSync.Complete.AI` | Meta package: `uSync.AI` plus `uSync.AI.Complete` | uSync.Complete |
| `uSync.AI.Complete` | Push/pull menus for AI items and agent tools for the publisher | uSync.Complete |

If you only use part of Umbraco.AI, install the individual packages instead of a meta package.

## Status

In development. Nothing is published yet. Settings, prompt and agent sync and the uSync agent
tools are in place; `uSync.AI.Complete` is not written yet.

## Building

```
dotnet build uSync.AI-CI.slnx
dotnet test uSync.AI-CI.slnx
```

`uSync.AI.slnx` adds the `uSync.AI.Site` test site. See `dist/build-package.ps1` for packaging.

## License

[MPL-2.0](LICENSE)

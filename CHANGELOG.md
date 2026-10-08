# Changelog

Notable changes to uSync.AI. Each release covers all seven packages (`uSync.AI`,
`uSync.AI.Sync`, `uSync.AI.Prompt`, `uSync.AI.Agent`, `uSync.AI.Tools`, `uSync.AI.Complete` and
`uSync.Complete.AI`), which ship together with the same version number.

## Unreleased

### Fixed

- The uSync dashboard showed the AI handlers as `aIConnections`, `aIGuardrails` and so on. They
  now show as "AI Connections", "AI Guardrails", etc.
  ([#24](https://github.com/Jumoo/uSync.AI/pull/24))
- AI connections had a blank icon on the uSync dashboard and in the uSync.Complete publisher.
  They now use the same plug icon as Umbraco.AI's Connections menu.
  ([#25](https://github.com/Jumoo/uSync.AI/pull/25))

## 17.0.0 - 2026-10-08

First release: uSync support for [Umbraco.AI](https://docs.umbraco.com/ai-in-umbraco) on
Umbraco 17.

Requires Umbraco 17.5+, Umbraco.AI 17.5.2+ and uSync 17.4.3+ (uSync.Complete 17.5.0+ for the
Complete packages).

### Added

- `uSync.AI.Sync`: syncs Umbraco.AI connections, guardrails, contexts, profiles and settings.
  Connection API keys are not written to disk by default; see the package readme.
- `uSync.AI.Prompt`: syncs Umbraco.AI prompts.
- `uSync.AI.Agent`: syncs Umbraco.AI agents, including their tools, contexts and per-user-group
  tool permissions.
- `uSync.AI.Tools`: Umbraco.AI agent tools for uSync report, export and import, checked against
  the acting user's uSync access.
- `uSync.AI.Complete`: push and pull AI items between servers with uSync.Complete (with their
  dependencies by default), and agent tools to publish content, pull content and take restore
  points.
- `uSync.AI` and `uSync.Complete.AI` meta packages.

# Security Policy

## Supported versions

This repo targets Umbraco 17 / .NET 10. All of its packages (`uSync.AI`, `uSync.AI.Sync`,
`uSync.AI.Prompt`, `uSync.AI.Agent`, `uSync.AI.Tools`, `uSync.AI.Complete`, `uSync.Complete.AI`)
are versioned and released together from `v17/main`.

| Version | Branch | Supported |
| --- | --- | --- |
| 17.x | `v17/main` | Yes |

## Reporting a vulnerability

Please **do not** open a public issue for a security problem.

Email **info@jumoo.co.uk** with a description of the issue, the version affected, and steps
to reproduce it. We'll acknowledge within a few working days and keep you updated as we
work on it.

These packages write Umbraco.AI connection settings to disk and let AI agents run uSync
imports, exports and uSync.Complete publishes. If the issue involves a secret ending up in a
uSync file, or an agent tool running with more access than the acting user has, say so - those
get sequenced first.

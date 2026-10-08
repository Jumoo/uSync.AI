# uSync.AI.Prompt

uSync handler and serializer for [Umbraco.AI.Prompt](https://docs.umbraco.com/ai-in-umbraco/add-ons/prompt)
prompts. A prompt is written to the `AI-Prompts` uSync folder when it is saved, and travels
between servers with the rest of your uSync files.

Most sites want the `uSync.AI` package, which installs this alongside settings and agent sync.

## What is synced

Everything on the prompt: name, description, instructions, display mode, option count, tags,
scope rules, and the profile, contexts and guardrails it uses.

A prompt keeps the same Id on every server. Its profile, contexts and guardrails are recorded
by Id and alias. If one of them isn't on the target server the prompt is still imported, with
that reference left off and a warning; the next import restores it once the item exists.

Prompts import after connections, guardrails, contexts, profiles and settings, so a full
import brings everything a prompt needs in first.

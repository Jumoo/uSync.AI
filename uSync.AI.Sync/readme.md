# uSync.AI.Sync

uSync handlers and serializers for [Umbraco.AI](https://docs.umbraco.com/ai-in-umbraco).
AI connections, guardrails, contexts, profiles and settings are written to the uSync folder
when they are saved, and travel between servers with the rest of your uSync files.

Most sites want the `uSync.AI` package, which installs this plus prompt and agent sync.

## What is synced

| Item | Folder | Notes |
| --- | --- | --- |
| Connections | `AI-Connections` | Secrets are never written, see below |
| Guardrails | `AI-Guardrails` | Including rules and their config |
| Contexts | `AI-Contexts` | Including resources and their settings |
| Profiles | `AI-Profiles` | Needs its connection on the target first |
| Settings | `AI-Settings` | Default profiles and the disclosure notice |

Items keep the same Id on every server, so anything that refers to a context, guardrail or
profile by Id (prompts, agents, content) still resolves after a sync. References are also
recorded by alias, so a file still imports onto a server where the item was created by hand.

## Connection secrets

A connection's API key is not written to disk. Any setting the provider marks as sensitive is
left out of the file, and its name is listed under `<Ignored>`:

```xml
<Settings><![CDATA[{
  "endpoint": "https://api.openai.com/v1"
}]]></Settings>
<Ignored>
  <Setting>apiKey</Setting>
</Ignored>
```

On import, an ignored setting keeps whatever value the target server already has. A new
connection arrives without its key: enter it once on that server and later syncs leave it alone.

To sync the key as well, store it in configuration and reference it. A value starting with `$`
names where the secret lives, so it is written as is:

```json
{ "apiKey": "$Umbraco:AI:Secrets:OpenAIApiKey" }
```

## Options

```json
{
  "uSync": {
    "AI": {
      "Connections": {
        "IgnoreSecretValues": true,
        "IgnoreSensitive": false,
        "IgnoreSettings": []
      }
    }
  }
}
```

- `IgnoreSecretValues` (default `true`): leave out the value of sensitive settings, unless it
  is a `$` configuration reference. Turning this off writes API keys to disk in plain text.
- `IgnoreSensitive` (default `false`): leave out sensitive settings entirely, `$` references
  included.
- `IgnoreSettings`: names of settings that are always left out.

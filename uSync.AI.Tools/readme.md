# uSync.AI.Tools

[Umbraco.AI](https://docs.umbraco.com/ai-in-umbraco) agent tools that run uSync. Give them to
an agent and an editor can ask "what would an import change?" or "export the settings".

Most sites want the `uSync.AI` package, which installs this alongside AI settings sync.

## Tools

| Tool | Scope | What it does |
| --- | --- | --- |
| `usync_list_handlers` | `usync-read` | Lists the uSync handlers and their groups |
| `usync_report` | `usync-read` | Compares the uSync files with the site. Changes nothing |
| `usync_export` | `usync-write` | Writes the site's items to the uSync folder |
| `usync_import` | `usync-write` | Applies the uSync files to the site |

Report, export and import take an optional handler group (`Settings`, `Content`, `AI`...) or
a list of handler aliases. Import can also be forced.

A tool is only available to an agent that has been given it, or its scope, in the agent's
settings. Nothing is switched on by installing this package.

## Who can run them

Each tool checks the backoffice user the agent is acting for before it does anything. The
agent's own settings can't override this.

| | Needs |
| --- | --- |
| Any tool | Access to the Settings section (or the uSync section), the same as the uSync dashboard |
| `usync_import` | Also an administrator |
| No signed-in user | Refused. An agent run from a schedule or an automation can't use these tools |

Export and import are marked destructive, so Umbraco.AI asks the user to approve each call,
and the prompt says what will run.

Only one uSync operation runs at a time. A second is refused, not queued.

## Options

```json
{
  "uSync": {
    "AI": {
      "Tools": {
        "RequireAdminForImport": true,
        "MaxChanges": 50
      }
    }
  }
}
```

- `RequireAdminForImport` (default `true`): set to `false` to let anyone with uSync access
  import through an agent, as the uSync dashboard does.
- `MaxChanges` (default `50`): how many changed or failed items a tool lists for the model.
  Counts are always complete.

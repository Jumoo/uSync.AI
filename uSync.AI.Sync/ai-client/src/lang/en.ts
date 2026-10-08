// Keys are named after the .NET type being synced (SyncAttempt.Succeed uses typeof(TObject).Name),
// matched by uSync's results/progress views (`uSync_{itemType}`, `uSync_group{groupName}`).
// AIPrompt and AIAgent are listed here, not in their own packages, so those packages don't each
// need a client bundle for two strings; the keys are unused when the add-on isn't installed.
const items = {
  AIConnection: "AI Connections",
  AIGuardrail: "AI Guardrails",
  AIContext: "AI Contexts",
  AIProfile: "AI Profiles",
  AISettings: "AI Settings",
  AIPrompt: "AI Prompts",
  AIAgent: "AI Agents",
};

export default {
  uSync: {
    groupAI: "AI",
    ...items,
  },
  usyncpublish: {
    ...items,
  },
  // Handler names in the uSync dashboard's progress box. uSync camel-cases the handler's display
  // name ("AI Connections" -> "aIConnections") and looks it up as `treeHeaders_{name}`, showing
  // the camel-cased name itself when there's no match.
  treeHeaders: {
    aIConnections: items.AIConnection,
    aIGuardrails: items.AIGuardrail,
    aIContexts: items.AIContext,
    aIProfiles: items.AIProfile,
    aISettings: items.AISettings,
    aIPrompts: items.AIPrompt,
    aIAgents: items.AIAgent,
  },
  // Labels for the tool scopes in uSync.AI.Tools, shown where an agent's tools are picked.
  // Umbraco.AI looks them up as `uaiToolScope_{camelCase(scopeId)}Label` and falls back to the
  // scope id. They live in this bundle so that package doesn't need a client of its own.
  uaiToolScope: {
    usyncReadLabel: "uSync (read)",
    usyncReadDescription: "Run uSync reports and list handlers. Changes nothing.",
    usyncWriteLabel: "uSync (write)",
    usyncWriteDescription: "Run uSync exports and imports. Imports change the site.",
  },
};

// Umbraco.AI's own entity types for the items uSync.AI syncs. Prompts and agents are listed
// even though their add-ons may not be installed: an action for an entity type nothing uses
// simply never appears.
//
// The "-root" types are deliberately left out: there is no single item to ask uSync about.
// Use the uSync dashboard to sync every AI item at once.
const aiEntityTypes = [
  "uai:connection",
  "uai:guardrail",
  "uai:context",
  "uai:profile",
  "uai:prompt",
  "uai:agent",
];

// These are our own actions, not uSync.Publisher's "publisher-push" / "publisher-pull" kinds.
// Those can't work for an entity type that isn't a valid UDI entity type - see
// ai-publish.action.ts. The icons and labels are the publisher's own, so the menu items read
// the same as they do everywhere else.
//
// The condition is the publisher's check that the user has the push or pull permission. It is
// registered by uSync.Publisher's client: without uSync.Complete an unknown condition is never
// met, so the actions stay hidden.
const permission = (allOf: string) => [{ alias: "usync.publisher.user.dashboard.condition", allOf: [allOf] }];

const actions = [
  {
    type: "entityAction",
    kind: "default",
    alias: "usync.ai.push.action",
    name: "Push AI item",
    weight: 21,
    api: () => import("./push.action.js"),
    forEntityTypes: aiEntityTypes,
    meta: {
      icon: "icon-arrow-right",
      label: "#usyncpublish_pushItems",
    },
    conditions: permission("uSync.UserPermission.Push"),
  },
  {
    type: "entityAction",
    kind: "default",
    alias: "usync.ai.pull.action",
    name: "Pull AI item",
    weight: 20,
    api: () => import("./pull.action.js"),
    forEntityTypes: aiEntityTypes,
    meta: {
      icon: "icon-arrow-left",
      label: "#usyncpublish_pullItems",
    },
    conditions: permission("uSync.UserPermission.Pull"),
  },
] as unknown as Array<UmbExtensionManifest>;

export const manifests: Array<UmbExtensionManifest> = [...actions];

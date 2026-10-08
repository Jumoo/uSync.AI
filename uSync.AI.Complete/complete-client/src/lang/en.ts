// Labels for this package's tool scopes, shown where an agent's tools are picked. Umbraco.AI
// looks them up as `uaiToolScope_{camelCase(scopeId)}Label` and falls back to the scope id.
export default {
  uaiToolScope: {
    usyncPublisherReadLabel: "uSync.Complete (read)",
    usyncPublisherReadDescription: "List the servers you can push to or pull from.",
    usyncPublisherLabel: "uSync.Complete (write)",
    usyncPublisherDescription: "Push content to another server, pull it from one, and take restore points.",
  },
};

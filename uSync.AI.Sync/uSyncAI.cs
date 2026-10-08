using uSync.BackOffice;
using uSync.BackOffice.Models;

namespace uSync.AI.Sync;

/// <summary>
/// Add-on descriptor for uSync.AI, shown in uSync's add-ons list.
/// </summary>
public class uSyncAI : ISyncAddOn
{
    public string Name => "AIEdition";

    public string Version => typeof(uSyncAI).Assembly.GetName().Version?.ToString(3) ?? "17.0.0";

    public string Icon => string.Empty;

    public string View => string.Empty;

    public string Alias => "usyncAI";

    public string DisplayName => "AIEdition";

    // after uSync.Forms (100) and uSync.Automate (110)
    public int SortOrder => 120;

    public const string GroupName = "AI";

    public const string GroupIcon = "icon-wand";

    /// <summary>
    /// UDI entity types. Deliberately the same strings Umbraco.AI.Deploy registers, so a UDI
    /// written by either package means the same thing.
    /// </summary>
    public static class EntityTypes
    {
        public const string Connection = "umbraco-ai-connection";
        public const string Guardrail = "umbraco-ai-guardrail";
        public const string Context = "umbraco-ai-context";
        public const string Profile = "umbraco-ai-profile";
        public const string Settings = "umbraco-ai-settings";
        public const string Prompt = "umbraco-ai-prompt";
        public const string Agent = "umbraco-ai-agent";
    }

    /// <summary>
    /// The entity types Umbraco.AI's own backoffice uses for its trees and workspaces. They are
    /// what an entity action is told it was run on, and are not valid UDI entity types.
    /// </summary>
    public static class ClientEntityTypes
    {
        public const string Connection = "uai:connection";
        public const string Guardrail = "uai:guardrail";
        public const string Context = "uai:context";
        public const string Profile = "uai:profile";
        public const string Prompt = "uai:prompt";
        public const string Agent = "uai:agent";
    }
}

/// <summary>
/// Handler priorities for uSync.AI. All sit above <see cref="uSyncConstants.Priorites.USYNC_RESERVED_UPPER"/>
/// and clear of uSync.Automate's (+20 to +25), ordered so everything an item references has
/// already been imported: profiles need connections, guardrails and contexts; settings, prompts
/// and agents need profiles.
/// </summary>
public static class uSyncAIPriorities
{
    public const int Connections = uSyncConstants.Priorites.USYNC_RESERVED_UPPER + 40;
    public const int Guardrails = uSyncConstants.Priorites.USYNC_RESERVED_UPPER + 42;
    public const int Contexts = uSyncConstants.Priorites.USYNC_RESERVED_UPPER + 44;
    public const int Profiles = uSyncConstants.Priorites.USYNC_RESERVED_UPPER + 46;
    public const int Settings = uSyncConstants.Priorites.USYNC_RESERVED_UPPER + 48;
    public const int Prompts = uSyncConstants.Priorites.USYNC_RESERVED_UPPER + 50;
    public const int Agents = uSyncConstants.Priorites.USYNC_RESERVED_UPPER + 52;
}

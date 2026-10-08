namespace uSync.AI.Tools;

/// <summary>
/// Options for the uSync agent tools, bound from <c>uSync:AI:Tools</c>.
/// </summary>
public class uSyncAIToolsOptions
{
    public const string Section = "uSync:AI:Tools";

    /// <summary>
    /// Only let administrators run an import through an agent. uSync's own dashboard lets anyone
    /// with access to it import; an agent deciding to import is a bigger step than a person
    /// pressing the button, so the default here is stricter.
    /// </summary>
    public bool RequireAdminForImport { get; set; } = true;

    /// <summary>
    /// How many changed or failed items a tool describes to the model. The counts are always
    /// complete; this only caps the list, which is paid for in tokens on every later turn.
    /// </summary>
    public int MaxChanges { get; set; } = 50;
}

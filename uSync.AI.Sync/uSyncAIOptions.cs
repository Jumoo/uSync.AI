namespace uSync.AI.Sync;

/// <summary>
/// Options for uSync.AI, bound from <c>uSync:AI</c>.
/// </summary>
public class uSyncAIOptions
{
    public const string Section = "uSync:AI";

    /// <summary>How connection settings are filtered before they are written to disk.</summary>
    public uSyncAIConnectionOptions Connections { get; set; } = new();
}

/// <summary>
/// Controls which connection settings reach a uSync file. By default no secret is ever written.
/// </summary>
public class uSyncAIConnectionOptions
{
    /// <summary>
    /// Leave out the value of every setting the provider marks <c>[AIField(IsSensitive = true)]</c>
    /// (API keys and the like), and any value that is already encrypted ("ENC:..."). Configuration
    /// references such as <c>$Umbraco:AI:Secrets:OpenAIApiKey</c> are not secrets and still sync.
    /// Turning this off writes API keys to disk in plain text.
    /// </summary>
    public bool IgnoreSecretValues { get; set; } = true;

    /// <summary>
    /// Leave out every setting the provider marks <c>[AIField(IsSensitive = true)]</c>, including
    /// configuration references.
    /// </summary>
    public bool IgnoreSensitive { get; set; }

    /// <summary>Setting names that are always left out. Takes precedence over the other two.</summary>
    public string[] IgnoreSettings { get; set; } = [];
}

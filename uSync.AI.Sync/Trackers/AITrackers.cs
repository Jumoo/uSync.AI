using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Profiles;
using Umbraco.AI.Core.Settings;
using uSync.Core.Serialization;
using uSync.Core.Tracking;

namespace uSync.AI.Sync.Trackers;

public class AIConnectionTracker : SyncXmlTrackAndMerger<AIConnection>, ISyncTracker<AIConnection>
{
    public AIConnectionTracker(SyncSerializerCollection serializers) : base(serializers) { }
}

public class AIGuardrailTracker : SyncXmlTrackAndMerger<AIGuardrail>, ISyncTracker<AIGuardrail>
{
    public AIGuardrailTracker(SyncSerializerCollection serializers) : base(serializers) { }
}

public class AIContextTracker : SyncXmlTrackAndMerger<AIContext>, ISyncTracker<AIContext>
{
    public AIContextTracker(SyncSerializerCollection serializers) : base(serializers) { }
}

public class AIProfileTracker : SyncXmlTrackAndMerger<AIProfile>, ISyncTracker<AIProfile>
{
    public AIProfileTracker(SyncSerializerCollection serializers) : base(serializers) { }
}

public class AISettingsTracker : SyncXmlTrackAndMerger<AISettings>, ISyncTracker<AISettings>
{
    public AISettingsTracker(SyncSerializerCollection serializers) : base(serializers) { }
}

using Umbraco.AI.Agent.Core.Agents;
using uSync.Core.Serialization;
using uSync.Core.Tracking;

namespace uSync.AI.Agent.Trackers;

public class AIAgentTracker : SyncXmlTrackAndMerger<AIAgent>, ISyncTracker<AIAgent>
{
    public AIAgentTracker(SyncSerializerCollection serializers) : base(serializers) { }
}

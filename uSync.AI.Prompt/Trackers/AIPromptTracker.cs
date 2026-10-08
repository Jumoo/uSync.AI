using Umbraco.AI.Prompt.Core.Prompts;
using uSync.Core.Serialization;
using uSync.Core.Tracking;

namespace uSync.AI.Prompt.Trackers;

public class AIPromptTracker : SyncXmlTrackAndMerger<AIPrompt>, ISyncTracker<AIPrompt>
{
    public AIPromptTracker(SyncSerializerCollection serializers) : base(serializers) { }
}

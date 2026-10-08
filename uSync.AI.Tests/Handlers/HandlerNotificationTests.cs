using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.AI.Prompt.Core.Prompts;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Strings;
using uSync.AI.Prompt.Handlers;
using uSync.AI.Prompt.Services;
using uSync.AI.Sync;
using uSync.AI.Sync.Notifications;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers;
using uSync.Core;
using uSync.Core.Serialization;

namespace uSync.AI.Tests.Handlers;

/// <summary>
/// Every AI handler tells the rest of the site when one of its items changes, so caches outside
/// uSync (uSync.Complete's dependency cache) can forget it.
/// </summary>
[TestFixture]
public class HandlerNotificationTests
{
    private static readonly Guid PromptId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private Mock<IEventAggregator> _events = null!;
    private Mock<IAIPromptService> _prompts = null!;
    private AIPromptHandler _handler = null!;

    [OneTimeSetUp]
    public void RegisterUdiTypes() => UdiParser.RegisterUdiType(uSyncAI.EntityTypes.Prompt, UdiType.GuidUdi);

    [SetUp]
    public void SetUp()
    {
        _events = new Mock<IEventAggregator>();
        _prompts = new Mock<IAIPromptService>();

        var serializer = new Mock<ISyncSerializer<AIPrompt>>();
        serializer.Setup(x => x.ItemType).Returns(nameof(AIPrompt));

        var itemFactory = new Mock<ISyncItemFactory>();
        itemFactory.Setup(x => x.GetSerializers<AIPrompt>()).Returns([serializer.Object]);
        itemFactory.Setup(x => x.GetTrackers<AIPrompt>()).Returns([]);

        var config = new Mock<ISyncConfigService>();
        config.Setup(x => x.Settings).Returns(new uSyncSettings());
        config.Setup(x => x.GetDefaultSetSettings()).Returns(new uSyncHandlerSetSettings());
        config.Setup(x => x.GetFolders()).Returns([]);

        // paused, as uSync is during an import: the handler writes no file, but still has to say
        // the item changed, or a server that receives a push keeps its stale cache.
        var mutex = new Mock<ISyncEventService>();
        mutex.Setup(x => x.IsPaused).Returns(true);

        _handler = new AIPromptHandler(
            NullLogger<SyncHandlerRoot<AIPrompt, AIPrompt>>.Instance,
            AppCaches.NoCache,
            Mock.Of<IShortStringHelper>(),
            Mock.Of<ISyncFileService>(),
            mutex.Object,
            config.Object,
            itemFactory.Object,
            new SyncAIPendingDeletes(),
            _events.Object,
            new SyncAIPromptService(_prompts.Object));
    }

    private void VerifyChanged()
        => _events.Verify(x => x.PublishAsync(
                It.Is<SyncAIItemChangedNotification>(n => n.Udi.Equals(Udi.Create(uSyncAI.EntityTypes.Prompt, PromptId))),
                It.IsAny<CancellationToken>()),
            Times.Once);

    [Test]
    public async Task Saving_PublishesThatTheItemChanged()
    {
        var prompt = new AIPrompt { Alias = "summarise", Name = "Summarise", Instructions = "x" }.WithId(PromptId);

        await _handler.HandleAsync(new AIPromptSavedNotification(prompt, new EventMessages()), CancellationToken.None);

        VerifyChanged();
    }

    [Test]
    public async Task Deleting_PublishesThatTheItemChanged()
    {
        await _handler.HandleAsync(new AIPromptDeletedNotification(PromptId, new EventMessages()), CancellationToken.None);

        VerifyChanged();
    }
}

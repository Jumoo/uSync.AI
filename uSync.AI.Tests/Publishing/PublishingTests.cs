using Microsoft.Extensions.Options;
using Moq;
using Umbraco.AI.Agent.Core.Agents;
using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Profiles;
using Umbraco.AI.Core.Providers;
using Umbraco.AI.Core.Settings;
using Umbraco.AI.Prompt.Core.Prompts;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using uSync.AI.Agent.Publishing;
using uSync.AI.Agent.Services;
using uSync.AI.Prompt.Publishing;
using uSync.AI.Prompt.Services;
using uSync.AI.Sync;
using uSync.AI.Sync.Publishing;
using uSync.AI.Sync.Services;
using uSync.Core.Dependency;
using uSync.Core.Sync;

namespace uSync.AI.Tests.Publishing;

/// <summary>
/// The item managers and dependency checkers uSync.Complete uses to push and pull AI items.
/// </summary>
[TestFixture]
public class PublishingTests
{
    private static readonly Guid ConnectionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GuardrailId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ContextId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherContextId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ItemId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private const DependencyFlags WithDependencies = DependencyFlags.IncludeDependencies;

    private Mock<IAIConnectionService> _connections = null!;
    private Mock<IAIProfileService> _profiles = null!;
    private SyncAIService _aiService = null!;
    private SyncAIDependencies _dependencies = null!;

    [OneTimeSetUp]
    public void RegisterUdiTypes()
    {
        foreach (var type in new[]
        {
            uSyncAI.EntityTypes.Connection, uSyncAI.EntityTypes.Guardrail, uSyncAI.EntityTypes.Context,
            uSyncAI.EntityTypes.Profile, uSyncAI.EntityTypes.Prompt, uSyncAI.EntityTypes.Agent,
        })
        {
            UdiParser.RegisterUdiType(type, UdiType.GuidUdi);
        }
    }

    [SetUp]
    public void SetUp()
    {
        _connections = new Mock<IAIConnectionService>();
        _profiles = new Mock<IAIProfileService>();
        var guardrails = new Mock<IAIGuardrailService>();
        var contexts = new Mock<IAIContextService>();

        var connection = new AIConnection { Alias = "open-ai", Name = "Open AI", ProviderId = "openai" }.WithId(ConnectionId);
        _connections.Setup(x => x.GetConnectionAsync(ConnectionId, It.IsAny<CancellationToken>())).ReturnsAsync(connection);
        _connections.Setup(x => x.GetConnectionsAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([connection]);

        _profiles.Setup(x => x.GetProfileAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(Profile());
        guardrails.Setup(x => x.GetGuardrailAsync(GuardrailId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGuardrail { Alias = "no-pii", Name = "No PII" }.WithId(GuardrailId));
        contexts.Setup(x => x.GetContextAsync(ContextId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIContext { Alias = "brand", Name = "Brand" }.WithId(ContextId));
        contexts.Setup(x => x.GetContextAsync(OtherContextId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIContext { Alias = "legal", Name = "Legal" }.WithId(OtherContextId));

        _aiService = new SyncAIService(_connections.Object, guardrails.Object, contexts.Object, _profiles.Object,
            Mock.Of<IAISettingsService>(), new AIProviderCollection(() => []));
        _dependencies = Dependencies(new uSyncAIOptions());
    }

    private SyncAIDependencies Dependencies(uSyncAIOptions options)
        => new(_aiService, Mock.Of<IOptionsMonitor<uSyncAIOptions>>(x => x.CurrentValue == options));

    private static AIProfile Profile() => new AIProfile
    {
        Alias = "default-chat",
        Name = "Default chat",
        ConnectionId = ConnectionId,
        Settings = new AIChatProfileSettings { ContextIds = [ContextId], GuardrailIds = [GuardrailId] },
    }.WithId(ProfileId);

    private static string[] Types(IEnumerable<uSyncDependency> items)
        => [.. items.OrderBy(x => x.Order).Select(x => $"{x.Udi!.EntityType}:{((GuidUdi)x.Udi).Guid}")];

    // ---- item managers

    [Test]
    public void ItemManager_AnswersToBothTheUdiTypeAndTheBackofficeType()
    {
        ISyncItemManager manager = new AIConnectionItemManager(_aiService);

        Assert.That(manager.EntityTypes, Is.EqualTo(new[] { "umbraco-ai-connection", "uai:connection" }));
    }

    [Test]
    public async Task ItemManager_TurnsABackofficeIdIntoAUdiTheHandlerUnderstands()
    {
        ISyncItemManager manager = new AIConnectionItemManager(_aiService);

        var entity = await manager.GetSyncEntityAsync(ConnectionId.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(entity!.Name, Is.EqualTo("Open AI"));
            Assert.That(entity.Udi, Is.EqualTo(Udi.Create(uSyncAI.EntityTypes.Connection, ConnectionId)));
        });
    }

    [Test]
    public async Task ItemManager_UnknownOrInvalidId_ReturnsNothing()
    {
        ISyncItemManager manager = new AIConnectionItemManager(_aiService);

        Assert.Multiple(async () =>
        {
            Assert.That(await manager.GetSyncEntityAsync(Guid.NewGuid().ToString()), Is.Null);
            Assert.That(await manager.GetSyncEntityAsync("not-a-guid"), Is.Null);
        });
        await Task.CompletedTask;
    }

    [Test]
    public async Task ItemManager_ASingleItem_IsJustThatItem()
    {
        ISyncItemManager manager = new AIConnectionItemManager(_aiService);
        var item = new SyncItem(DependencyFlags.IncludeChildren)
        {
            Name = "Open AI",
            Udi = Udi.Create(uSyncAI.EntityTypes.Connection, ConnectionId),
        };

        var items = await manager.GetItemsAsync(item);

        Assert.That(items.Select(x => x.Udi), Is.EqualTo(new[] { item.Udi }));
    }

    [Test]
    public async Task ItemManager_TheRootWithChildren_IsEveryItem()
    {
        ISyncItemManager manager = new AIConnectionItemManager(_aiService);
        var root = new SyncItem(DependencyFlags.IncludeChildren)
        {
            Name = "root",
            Udi = Udi.Create(uSyncAI.EntityTypes.Connection),
        };

        var items = (await manager.GetItemsAsync(root)).Where(x => !x.Udi.IsRoot).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(x => x.Udi), Is.EqualTo(new[] { Udi.Create(uSyncAI.EntityTypes.Connection, ConnectionId) }));
            Assert.That(items.Single().Flags.HasFlag(DependencyFlags.IncludeChildren), Is.False);
        });
    }

    // ---- dependency checkers

    [Test]
    public async Task Profile_BringsItsConnectionGuardrailsAndContexts()
    {
        var result = await new AIProfileDependencyChecker(_dependencies).GetDependenciesAsync(Profile(), WithDependencies);

        Assert.That(Types(result), Is.EqualTo(new[]
        {
            $"umbraco-ai-connection:{ConnectionId}",
            $"umbraco-ai-guardrail:{GuardrailId}",
            $"umbraco-ai-context:{ContextId}",
            $"umbraco-ai-profile:{ProfileId}",
        }));
    }

    [Test]
    public async Task Profile_WithoutTheDependenciesFlag_StillBringsItsDependencies_ByDefault()
    {
        // the publisher's "include dependencies" is off by default; an AI item without its
        // dependencies arrives with its references to them dropped.
        var result = await new AIProfileDependencyChecker(_dependencies).GetDependenciesAsync(Profile(), DependencyFlags.None);

        Assert.That(Types(result), Is.EqualTo(new[]
        {
            $"umbraco-ai-connection:{ConnectionId}",
            $"umbraco-ai-guardrail:{GuardrailId}",
            $"umbraco-ai-context:{ContextId}",
            $"umbraco-ai-profile:{ProfileId}",
        }));
    }

    [Test]
    public async Task Profile_WithoutTheDependenciesFlag_IsJustItself_WhenAlwaysIncludeIsOff()
    {
        var dependencies = Dependencies(new uSyncAIOptions { Publishing = { AlwaysIncludeDependencies = false } });

        var result = await new AIProfileDependencyChecker(dependencies).GetDependenciesAsync(Profile(), DependencyFlags.None);

        Assert.That(Types(result), Is.EqualTo(new[] { $"umbraco-ai-profile:{ProfileId}" }));
    }

    [Test]
    public async Task Prompt_WithoutTheDependenciesFlag_BringsItsProfile_ByDefault()
    {
        var prompt = new AIPrompt { Alias = "summarise", Name = "Summarise", Instructions = "x", ProfileId = ProfileId }.WithId(ItemId);

        var result = await new AIPromptDependencyChecker(_dependencies).GetDependenciesAsync(prompt, DependencyFlags.None);

        Assert.That(Types(result), Does.Contain($"umbraco-ai-profile:{ProfileId}"));
    }

    [Test]
    public async Task Prompt_BringsItsDirectDependencies_WithEachItemOnce()
    {
        var prompt = new AIPrompt
        {
            Alias = "summarise",
            Name = "Summarise",
            Instructions = "Do it.",
            ProfileId = ProfileId,
            // the same context and guardrail the profile already uses, plus one more
            ContextIds = [ContextId, OtherContextId],
            GuardrailIds = [GuardrailId],
        }.WithId(ItemId);

        var result = await new AIPromptDependencyChecker(_dependencies).GetDependenciesAsync(prompt, WithDependencies);

        // not the profile's connection: uSync.Complete asks the profile's checker for that, so
        // it caches it against the profile and a change to the profile is picked up.
        Assert.That(Types(result), Is.EquivalentTo(new[]
        {
            $"umbraco-ai-guardrail:{GuardrailId}",
            $"umbraco-ai-context:{ContextId}",
            $"umbraco-ai-context:{OtherContextId}",
            $"umbraco-ai-profile:{ProfileId}",
            $"umbraco-ai-prompt:{ItemId}",
        }));
        Assert.That(result.Last().Udi!.EntityType, Is.Not.Null);
        Assert.That(result.MaxBy(x => x.Order)!.Udi!.EntityType, Is.EqualTo("umbraco-ai-prompt"), "the prompt itself goes last");
    }

    [Test]
    public async Task Prompt_WithNoProfile_BringsOnlyWhatItNames()
    {
        var prompt = new AIPrompt { Alias = "plain", Name = "Plain", Instructions = "Do it.", ContextIds = [OtherContextId] }.WithId(ItemId);

        var result = await new AIPromptDependencyChecker(_dependencies).GetDependenciesAsync(prompt, WithDependencies);

        Assert.That(Types(result), Is.EqualTo(new[] { $"umbraco-ai-context:{OtherContextId}", $"umbraco-ai-prompt:{ItemId}" }));
    }

    [Test]
    public async Task Dependency_ThatNoLongerExists_IsLeftOut()
    {
        var prompt = new AIPrompt { Alias = "plain", Name = "Plain", Instructions = "Do it.", ContextIds = [Guid.NewGuid()] }.WithId(ItemId);

        var result = await new AIPromptDependencyChecker(_dependencies).GetDependenciesAsync(prompt, WithDependencies);

        Assert.That(Types(result), Is.EqualTo(new[] { $"umbraco-ai-prompt:{ItemId}" }));
    }

    [Test]
    public async Task StandardAgent_BringsItsProfileGuardrailsAndConfigContexts()
    {
        var agent = new AIAgent
        {
            Alias = "helper",
            Name = "Helper",
            ProfileId = ProfileId,
            GuardrailIds = [GuardrailId],
            Config = new AIStandardAgentConfig { ContextIds = [OtherContextId] },
        }.WithId(ItemId);

        var result = await new AIAgentDependencyChecker(_dependencies).GetDependenciesAsync(agent, WithDependencies);

        Assert.That(Types(result), Is.EquivalentTo(new[]
        {
            $"umbraco-ai-guardrail:{GuardrailId}",
            $"umbraco-ai-context:{OtherContextId}",
            $"umbraco-ai-profile:{ProfileId}",
            $"umbraco-ai-agent:{ItemId}",
        }));
        Assert.That(result.MaxBy(x => x.Order)!.Udi!.EntityType, Is.EqualTo("umbraco-ai-agent"), "the agent itself goes last");
    }

    [Test]
    public void PromptAndAgentManagers_UseTheirOwnTypes()
    {
        ISyncItemManager prompts = new AIPromptItemManager(new SyncAIPromptService(Mock.Of<IAIPromptService>()));
        ISyncItemManager agents = new AIAgentItemManager(new SyncAIAgentService(Mock.Of<IAIAgentService>(), Mock.Of<IUserGroupService>()));

        Assert.Multiple(() =>
        {
            Assert.That(prompts.EntityTypes, Is.EqualTo(new[] { "umbraco-ai-prompt", "uai:prompt" }));
            Assert.That(agents.EntityTypes, Is.EqualTo(new[] { "umbraco-ai-agent", "uai:agent" }));
        });
    }
}

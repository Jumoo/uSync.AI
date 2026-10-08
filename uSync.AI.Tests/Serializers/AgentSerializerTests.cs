using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.AI.Agent.Core.Agents;
using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Profiles;
using Umbraco.AI.Core.Providers;
using Umbraco.AI.Core.Settings;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using uSync.AI.Agent.Serializers;
using uSync.AI.Agent.Services;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Serialization;

namespace uSync.AI.Tests.Serializers;

[TestFixture]
public class AgentSerializerTests
{
    private static readonly Guid AgentId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GuardrailId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ContextId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid EditorsOnSource = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private Mock<IAIAgentService> _agents = null!;
    private Mock<IUserGroupService> _userGroups = null!;
    private Mock<IAIProfileService> _profiles = null!;
    private Mock<IAIGuardrailService> _guardrails = null!;
    private Mock<IAIContextService> _contexts = null!;
    private AIAgentSerializer _serializer = null!;
    private SyncSerializerOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _agents = new Mock<IAIAgentService>();
        _userGroups = new Mock<IUserGroupService>();
        _profiles = new Mock<IAIProfileService>();
        _guardrails = new Mock<IAIGuardrailService>();
        _contexts = new Mock<IAIContextService>();

        var aiService = new SyncAIService(
            Mock.Of<IAIConnectionService>(), _guardrails.Object, _contexts.Object, _profiles.Object,
            Mock.Of<IAISettingsService>(), new AIProviderCollection(() => []));

        _serializer = new AIAgentSerializer(
            NullLogger<SyncSerializerRoot<AIAgent>>.Instance,
            new SyncAIAgentService(_agents.Object, _userGroups.Object), aiService);
        _options = new SyncSerializerOptions();

        _profiles.Setup(x => x.GetProfileAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProfile { Alias = "default-chat", Name = "Default chat", ConnectionId = Guid.NewGuid() }.WithId(ProfileId));
        _guardrails.Setup(x => x.GetGuardrailAsync(GuardrailId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGuardrail { Alias = "no-pii", Name = "No PII" }.WithId(GuardrailId));
        _contexts.Setup(x => x.GetContextAsync(ContextId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIContext { Alias = "brand", Name = "Brand" }.WithId(ContextId));
        _userGroups.Setup(x => x.GetAsync(EditorsOnSource))
            .ReturnsAsync(Mock.Of<IUserGroup>(g => g.Key == EditorsOnSource && g.Alias == "editor"));
    }

    private static AIAgent StandardAgent() => new AIAgent
    {
        Alias = "content-helper",
        Name = "Content helper",
        Description = "Helps editors",
        AgentType = AIAgentType.Standard,
        ProfileId = ProfileId,
        GuardrailIds = [GuardrailId],
        SurfaceIds = ["copilot", "automate"],
        IsActive = false,
        Scope = new AIAgentScope { AllowRules = [new AIAgentScopeRule { Sections = ["content"], EntityTypes = ["document"] }] },
        Config = new AIStandardAgentConfig
        {
            Instructions = "You help editors. Keep answers <short> & plain.",
            ContextIds = [ContextId],
            AllowedToolIds = ["usync_report", "search_umbraco"],
            AllowedToolScopeIds = ["content-read"],
            OutputSchema = JsonSerializer.SerializeToElement(new { type = "object", required = new[] { "summary" } }),
            UserGroupPermissions = new Dictionary<Guid, AIAgentUserGroupPermissions>
            {
                [EditorsOnSource] = new()
                {
                    AllowedToolIds = ["usync_export"],
                    AllowedToolScopeIds = ["usync-read"],
                    DeniedToolIds = ["usync_import"],
                    DeniedToolScopeIds = ["usync-write"],
                },
            },
        },
    }.WithId(AgentId);

    [Test]
    public async Task StandardAgent_RoundTrips()
    {
        var xml = (await _serializer.SerializeAsync(StandardAgent(), _options)).Item!;

        // through text, as it would be on disk
        var result = await _serializer.DeserializeAsync(XElement.Parse(xml.ToString()), _options);

        Assert.That(result.Success, Is.True, result.Message);
        var item = result.Item!;
        var config = (AIStandardAgentConfig)item.Config!;
        var groupPermissions = config.UserGroupPermissions[EditorsOnSource];
        Assert.Multiple(() =>
        {
            Assert.That(item.Id, Is.EqualTo(AgentId));
            Assert.That(item.Alias, Is.EqualTo("content-helper"));
            Assert.That(item.Description, Is.EqualTo("Helps editors"));
            Assert.That(item.AgentType, Is.EqualTo(AIAgentType.Standard));
            Assert.That(item.IsActive, Is.False);
            Assert.That(item.ProfileId, Is.EqualTo(ProfileId));
            Assert.That(item.GuardrailIds, Is.EqualTo(new[] { GuardrailId }));
            Assert.That(item.SurfaceIds, Is.EqualTo(new[] { "automate", "copilot" }));
            Assert.That(item.Scope!.AllowRules.Single().Sections, Is.EqualTo(new[] { "content" }));
            Assert.That(item.Scope.AllowRules.Single().EntityTypes, Is.EqualTo(new[] { "document" }));

            Assert.That(config.Instructions, Is.EqualTo("You help editors. Keep answers <short> & plain."));
            Assert.That(config.ContextIds, Is.EqualTo(new[] { ContextId }));
            Assert.That(config.AllowedToolIds, Is.EqualTo(new[] { "search_umbraco", "usync_report" }));
            Assert.That(config.AllowedToolScopeIds, Is.EqualTo(new[] { "content-read" }));
            Assert.That(config.OutputSchema!.Value.GetProperty("type").GetString(), Is.EqualTo("object"));

            Assert.That(groupPermissions.AllowedToolIds, Is.EqualTo(new[] { "usync_export" }));
            Assert.That(groupPermissions.AllowedToolScopeIds, Is.EqualTo(new[] { "usync-read" }));
            Assert.That(groupPermissions.DeniedToolIds, Is.EqualTo(new[] { "usync_import" }));
            Assert.That(groupPermissions.DeniedToolScopeIds, Is.EqualTo(new[] { "usync-write" }));
            Assert.That(result.Details, Is.Empty);
        });
        _agents.Verify(x => x.SaveAgentAsync(item, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UserGroupPermissions_FollowTheGroupsAlias_WhenItsKeyDiffersOnTheTarget()
    {
        var xml = (await _serializer.SerializeAsync(StandardAgent(), _options)).Item!;

        var editorsOnTarget = Guid.NewGuid();
        _userGroups.Reset();
        _userGroups.Setup(x => x.GetAsync("editor"))
            .ReturnsAsync(Mock.Of<IUserGroup>(g => g.Key == editorsOnTarget && g.Alias == "editor"));

        var result = await _serializer.DeserializeAsync(xml, _options);

        var config = (AIStandardAgentConfig)result.Item!.Config!;
        Assert.Multiple(() =>
        {
            Assert.That(config.UserGroupPermissions.Keys, Is.EqualTo(new[] { editorsOnTarget }));
            Assert.That(config.UserGroupPermissions[editorsOnTarget].DeniedToolIds, Is.EqualTo(new[] { "usync_import" }));
            Assert.That(result.Details, Is.Empty);
        });
    }

    [Test]
    public async Task UserGroupPermissions_ForAGroupTheTargetDoesNotHave_AreDroppedWithAWarning()
    {
        var xml = (await _serializer.SerializeAsync(StandardAgent(), _options)).Item!;
        _userGroups.Reset();

        var result = await _serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(((AIStandardAgentConfig)result.Item!.Config!).UserGroupPermissions, Is.Empty);
            Assert.That(result.Details!.Single().Path, Is.EqualTo("UserGroupPermissions"));
        });
    }

    [Test]
    public async Task MissingReferences_AreLeftOffWithAWarningEach()
    {
        var xml = (await _serializer.SerializeAsync(StandardAgent(), _options)).Item!;
        _profiles.Reset();
        _guardrails.Reset();
        _contexts.Reset();

        var result = await _serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Item!.ProfileId, Is.Null);
            Assert.That(result.Item.GuardrailIds, Is.Empty);
            Assert.That(((AIStandardAgentConfig)result.Item.Config!).ContextIds, Is.Empty);
            Assert.That(result.Details!.Count(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task OrchestratedAgent_RoundTrips()
    {
        var agent = new AIAgent
        {
            Alias = "pipeline",
            Name = "Pipeline",
            AgentType = AIAgentType.Orchestrated,
            Config = new AIOrchestratedAgentConfig
            {
                WorkflowId = "sequential",
                Settings = JsonSerializer.SerializeToElement(new { steps = new[] { "draft", "review" }, maxTurns = 4 }),
            },
        }.WithId(AgentId);

        var xml = (await _serializer.SerializeAsync(agent, _options)).Item!;
        var result = await _serializer.DeserializeAsync(XElement.Parse(xml.ToString()), _options);

        Assert.That(result.Success, Is.True, result.Message);
        var config = (AIOrchestratedAgentConfig)result.Item!.Config!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Item.AgentType, Is.EqualTo(AIAgentType.Orchestrated));
            Assert.That(config.WorkflowId, Is.EqualTo("sequential"));
            Assert.That(config.Settings!.Value.GetProperty("maxTurns").GetInt32(), Is.EqualTo(4));
            Assert.That(config.Settings.Value.GetProperty("steps").GetArrayLength(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task DifferentAgentType_Fails()
    {
        var xml = (await _serializer.SerializeAsync(StandardAgent(), _options)).Item!;

        _agents.Setup(x => x.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIAgent { Alias = "content-helper", Name = "Other", AgentType = AIAgentType.Orchestrated }.WithId(AgentId));

        var result = await _serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.False);
        _agents.Verify(x => x.SaveAgentAsync(It.IsAny<AIAgent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Serialize_IsStable_WhateverOrderListsArriveIn()
    {
        var a = StandardAgent();
        var b = StandardAgent();
        b.SurfaceIds = ["automate", "copilot"];
        ((AIStandardAgentConfig)b.Config!).AllowedToolIds = ["search_umbraco", "usync_report"];

        var first = (await _serializer.SerializeAsync(a, _options)).Item!.ToString();
        var second = (await _serializer.SerializeAsync(b, _options)).Item!.ToString();

        Assert.That(second, Is.EqualTo(first));
    }
}

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Models;
using Umbraco.AI.Core.EditableModels;
using Umbraco.AI.Core.Profiles;
using Umbraco.AI.Core.Providers;
using Umbraco.AI.Core.Settings;
using uSync.AI.Sync;
using uSync.AI.Sync.Serializers;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Serialization;

namespace uSync.AI.Tests.Serializers;

/// <summary>
/// Each test serializes an entity, then deserializes the XML against a "target server" whose
/// services start empty, and checks the entity that would be saved there.
/// </summary>
[TestFixture]
public class SerializerRoundTripTests
{
    private static readonly Guid ConnectionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GuardrailId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ContextId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ChildId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private Mock<IAIConnectionService> _connections = null!;
    private Mock<IAIGuardrailService> _guardrails = null!;
    private Mock<IAIContextService> _contexts = null!;
    private Mock<IAIProfileService> _profiles = null!;
    private Mock<IAISettingsService> _settings = null!;
    private SyncAIService _service = null!;
    private SyncSerializerOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _connections = new Mock<IAIConnectionService>();
        _guardrails = new Mock<IAIGuardrailService>();
        _contexts = new Mock<IAIContextService>();
        _profiles = new Mock<IAIProfileService>();
        _settings = new Mock<IAISettingsService>();

        var provider = Mock.Of<IAIProvider>(x => x.Id == "openai" && x.SettingsType == typeof(FakeProviderSettings));
        _service = new SyncAIService(_connections.Object, _guardrails.Object, _contexts.Object, _profiles.Object, _settings.Object,
            new AIProviderCollection(() => [provider]));
        _options = new SyncSerializerOptions();
    }

    private sealed class FakeProviderSettings
    {
        [AIField(IsSensitive = true)]
        public string? ApiKey { get; set; }

        public string? Endpoint { get; set; }
    }

    private AIConnectionSerializer ConnectionSerializer(uSyncAIOptions? options = null)
        => new(NullLogger<SyncSerializerRoot<AIConnection>>.Instance, _service,
            Mock.Of<IOptionsMonitor<uSyncAIOptions>>(x => x.CurrentValue == (options ?? new uSyncAIOptions())));

    private static AIConnection Connection() => new AIConnection
    {
        Alias = "open-ai",
        Name = "Open AI",
        ProviderId = "openai",
        IsActive = false,
        Settings = new Dictionary<string, object?> { ["ApiKey"] = "sk-plain-text-secret", ["Endpoint"] = "https://api.example.com" },
    }.WithId(ConnectionId);

    [Test]
    public async Task Connection_RoundTrips_WithoutItsSecret()
    {
        var serializer = ConnectionSerializer();
        var xml = (await serializer.SerializeAsync(Connection(), _options)).Item!;

        Assert.That(xml.ToString(), Does.Not.Contain("sk-plain-text-secret"));
        Assert.That(xml.Element("Ignored")!.Elements("Setting").Select(x => x.Value), Is.EqualTo(new[] { "ApiKey" }));

        AIConnection? saved = null;
        _connections.Setup(x => x.SaveConnectionAsync(It.IsAny<AIConnection>(), It.IsAny<CancellationToken>()))
            .Callback<AIConnection, CancellationToken>((c, _) => saved = c)
            .ReturnsAsync((AIConnection c, CancellationToken _) => c);

        var result = await serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(saved!.Id, Is.EqualTo(ConnectionId));
            Assert.That(saved.Alias, Is.EqualTo("open-ai"));
            Assert.That(saved.Name, Is.EqualTo("Open AI"));
            Assert.That(saved.ProviderId, Is.EqualTo("openai"));
            Assert.That(saved.IsActive, Is.False);

            // the secret isn't in the file and this server has none, so it gets the
            // placeholder and the import says so.
            var settings = (Dictionary<string, object?>)saved.Settings!;
            Assert.That(settings["Endpoint"]!.ToString(), Is.EqualTo("https://api.example.com"));
            Assert.That(settings["ApiKey"]!.ToString(), Is.EqualTo(AIConnectionSettingsFilter.MissingValuePlaceholder));
            Assert.That(result.Details!.Single().Name, Is.EqualTo("ApiKey"));
        });
    }

    [Test]
    public async Task Connection_Import_KeepsTheSecretAlreadyOnTheTarget()
    {
        var serializer = ConnectionSerializer();
        var xml = (await serializer.SerializeAsync(Connection(), _options)).Item!;
        xml.Element("Info")!.Element("Name")!.Value = "Renamed";

        var target = Connection();
        target.Settings = new Dictionary<string, object?> { ["ApiKey"] = "ENC:target-secret", ["Endpoint"] = "https://old.example.com" };
        _connections.Setup(x => x.GetConnectionAsync(ConnectionId, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var result = await serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        var settings = (Dictionary<string, object?>)result.Item!.Settings!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Item.Name, Is.EqualTo("Renamed"));
            Assert.That(settings["ApiKey"]!.ToString(), Is.EqualTo("ENC:target-secret"));
            Assert.That(settings["Endpoint"]!.ToString(), Is.EqualTo("https://api.example.com"));
            Assert.That(result.Details, Is.Empty);
        });
    }

    [Test]
    public async Task Connection_FoundByAlias_WhenTheKeyDiffers()
    {
        var serializer = ConnectionSerializer();
        var xml = (await serializer.SerializeAsync(Connection(), _options)).Item!;

        var target = Connection().WithId(Guid.NewGuid());
        target.Name = "Made by hand";
        _connections.Setup(x => x.GetConnectionByAliasAsync("open-ai", It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var result = await serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Item, Is.SameAs(target));
            Assert.That(result.Item!.Name, Is.EqualTo("Open AI"));
        });
    }

    [Test]
    public async Task Connection_DifferentProvider_Fails()
    {
        var serializer = ConnectionSerializer();
        var xml = (await serializer.SerializeAsync(Connection(), _options)).Item!;

        var target = new AIConnection { Alias = "open-ai", Name = "Other", ProviderId = "anthropic" }.WithId(ConnectionId);
        _connections.Setup(x => x.GetConnectionAsync(ConnectionId, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var result = await serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.False);
        _connections.Verify(x => x.SaveConnectionAsync(It.IsAny<AIConnection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Guardrail_RoundTrips_WithRules()
    {
        var serializer = new AIGuardrailSerializer(NullLogger<SyncSerializerRoot<AIGuardrail>>.Instance, _service);

        var guardrail = new AIGuardrail
        {
            Alias = "no-pii",
            Name = "No PII",
            Rules =
            [
                new AIGuardrailRule
                {
                    Name = "Second", EvaluatorId = "regex", SortOrder = 1, Phase = AIGuardrailPhase.PreGenerate,
                    Action = AIGuardrailAction.Warn, Config = JsonSerializer.SerializeToElement(new { pattern = "\\d+", flags = "i" }),
                },
                new AIGuardrailRule { Name = "First", EvaluatorId = "pii", SortOrder = 0 }.WithId(ChildId),
            ],
        }.WithId(GuardrailId);

        var xml = (await serializer.SerializeAsync(guardrail, _options)).Item!;
        var result = await serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        var item = result.Item!;
        Assert.Multiple(() =>
        {
            Assert.That(item.Id, Is.EqualTo(GuardrailId));
            Assert.That(item.Alias, Is.EqualTo("no-pii"));
            Assert.That(item.Rules.Select(x => x.Name), Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(item.Rules[0].Id, Is.EqualTo(ChildId));
            Assert.That(item.Rules[0].EvaluatorId, Is.EqualTo("pii"));
            Assert.That(item.Rules[0].Config, Is.Null);
            Assert.That(item.Rules[1].Phase, Is.EqualTo(AIGuardrailPhase.PreGenerate));
            Assert.That(item.Rules[1].Action, Is.EqualTo(AIGuardrailAction.Warn));
            Assert.That(item.Rules[1].Config!.Value.GetProperty("pattern").GetString(), Is.EqualTo("\\d+"));
        });
        _guardrails.Verify(x => x.SaveGuardrailAsync(item, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Context_RoundTrips_WithResources()
    {
        var serializer = new AIContextSerializer(NullLogger<SyncSerializerRoot<AIContext>>.Instance, _service);

        var context = new AIContext
        {
            Alias = "brand",
            Name = "Brand voice",
            Resources =
            [
                new AIContextResource
                {
                    Name = "Tone", ResourceTypeId = "text", Description = "How we write", SortOrder = 0,
                    InjectionMode = AIContextResourceInjectionMode.OnDemand,
                    Settings = JsonSerializer.SerializeToElement(new { content = "Plain <b>English</b> & short." }),
                }.WithId(ChildId),
            ],
        }.WithId(ContextId);

        var xml = (await serializer.SerializeAsync(context, _options)).Item!;

        // through text, as it would be on disk
        var result = await serializer.DeserializeAsync(XElement.Parse(xml.ToString()), _options);

        Assert.That(result.Success, Is.True, result.Message);
        var resource = result.Item!.Resources.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Item.Id, Is.EqualTo(ContextId));
            Assert.That(resource.Id, Is.EqualTo(ChildId));
            Assert.That(resource.Name, Is.EqualTo("Tone"));
            Assert.That(resource.ResourceTypeId, Is.EqualTo("text"));
            Assert.That(resource.Description, Is.EqualTo("How we write"));
            Assert.That(resource.InjectionMode, Is.EqualTo(AIContextResourceInjectionMode.OnDemand));
            Assert.That(((JsonElement)resource.Settings!).GetProperty("content").GetString(), Is.EqualTo("Plain <b>English</b> & short."));
        });
    }

    private AIProfile Profile() => new AIProfile
    {
        Alias = "default-chat",
        Name = "Default chat",
        Capability = AICapability.Chat,
        ConnectionId = ConnectionId,
        Model = new AIModelRef("openai", "gpt-5"),
        Tags = ["zeta", "alpha"],
        Settings = new AIChatProfileSettings
        {
            Temperature = 0.4f, MaxTokens = 2000, SystemPromptTemplate = "Be brief.",
            ContextIds = [ContextId], GuardrailIds = [GuardrailId],
        },
    }.WithId(ProfileId);

    [Test]
    public async Task Profile_RoundTrips_AndResolvesItsConnectionByKey()
    {
        var serializer = new AIProfileSerializer(NullLogger<SyncSerializerRoot<AIProfile>>.Instance, _service);
        _connections.Setup(x => x.GetConnectionAsync(ConnectionId, It.IsAny<CancellationToken>())).ReturnsAsync(Connection());

        var xml = (await serializer.SerializeAsync(Profile(), _options)).Item!;
        Assert.That(xml.Element("Info")!.Element("Connection")!.Value, Is.EqualTo("open-ai"));

        var result = await serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        var item = result.Item!;
        var settings = (AIChatProfileSettings)item.Settings!;
        Assert.Multiple(() =>
        {
            Assert.That(item.Id, Is.EqualTo(ProfileId));
            Assert.That(item.Capability, Is.EqualTo(AICapability.Chat));
            Assert.That(item.ConnectionId, Is.EqualTo(ConnectionId));
            Assert.That(item.Model.ProviderId, Is.EqualTo("openai"));
            Assert.That(item.Model.ModelId, Is.EqualTo("gpt-5"));
            Assert.That(item.Tags, Is.EqualTo(new[] { "alpha", "zeta" }));
            Assert.That(settings.Temperature, Is.EqualTo(0.4f));
            Assert.That(settings.MaxTokens, Is.EqualTo(2000));
            Assert.That(settings.SystemPromptTemplate, Is.EqualTo("Be brief."));
            Assert.That(settings.ContextIds, Is.EqualTo(new[] { ContextId }));
            Assert.That(settings.GuardrailIds, Is.EqualTo(new[] { GuardrailId }));
        });
    }

    [Test]
    public async Task Profile_ResolvesItsConnectionByAlias_WhenTheKeyIsDifferentOnTheTarget()
    {
        var serializer = new AIProfileSerializer(NullLogger<SyncSerializerRoot<AIProfile>>.Instance, _service);
        _connections.Setup(x => x.GetConnectionAsync(ConnectionId, It.IsAny<CancellationToken>())).ReturnsAsync(Connection());
        var xml = (await serializer.SerializeAsync(Profile(), _options)).Item!;

        var targetConnectionId = Guid.NewGuid();
        _connections.Reset();
        _connections.Setup(x => x.GetConnectionByAliasAsync("open-ai", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Connection().WithId(targetConnectionId));

        var result = await serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Item!.ConnectionId, Is.EqualTo(targetConnectionId));
    }

    [Test]
    public async Task Profile_WithoutItsConnection_Fails()
    {
        var serializer = new AIProfileSerializer(NullLogger<SyncSerializerRoot<AIProfile>>.Instance, _service);
        var xml = (await serializer.SerializeAsync(Profile(), _options)).Item!;

        var result = await serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.False);
        _profiles.Verify(x => x.SaveProfileAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Settings_RoundTrip_AndWarnAboutAMissingProfile()
    {
        var serializer = new AISettingsSerializer(NullLogger<SyncSerializerRoot<AISettings>>.Instance, _service);
        var missingProfileId = Guid.NewGuid();

        _profiles.Setup(x => x.GetProfileAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(Profile());

        var source = new AISettings
        {
            DefaultChatProfileId = ProfileId,
            DefaultEmbeddingProfileId = missingProfileId,
            DisclosureNoticeMode = AIDisclosureNoticeMode.Off,
        };

        var xml = (await serializer.SerializeAsync(source, _options)).Item!;

        var target = new AISettings { ClassifierChatProfileId = Guid.NewGuid() };
        _settings.Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var result = await serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(target.DefaultChatProfileId, Is.EqualTo(ProfileId));
            Assert.That(target.DefaultEmbeddingProfileId, Is.Null);
            Assert.That(target.ClassifierChatProfileId, Is.Null);
            Assert.That(target.DisclosureNoticeMode, Is.EqualTo(AIDisclosureNoticeMode.Off));
            Assert.That(result.Details!.Count(), Is.EqualTo(1));
        });
        _settings.Verify(x => x.SaveSettingsAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Serialize_IsStable_WhateverOrderTheDataArrivesIn()
    {
        var serializer = ConnectionSerializer(new uSyncAIOptions { Connections = { IgnoreSecretValues = false } });

        var a = Connection();
        var b = Connection();
        b.Settings = new Dictionary<string, object?> { ["Endpoint"] = "https://api.example.com", ["ApiKey"] = "sk-plain-text-secret" };

        var first = (await serializer.SerializeAsync(a, _options)).Item!.ToString();
        var second = (await serializer.SerializeAsync(b, _options)).Item!.ToString();

        Assert.That(second, Is.EqualTo(first));
    }
}

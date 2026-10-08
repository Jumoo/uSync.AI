using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Profiles;
using Umbraco.AI.Core.Providers;
using Umbraco.AI.Core.Settings;
using Umbraco.AI.Prompt.Core.Prompts;
using uSync.AI.Prompt.Serializers;
using uSync.AI.Prompt.Services;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Serialization;

namespace uSync.AI.Tests.Serializers;

[TestFixture]
public class PromptSerializerTests
{
    private static readonly Guid PromptId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GuardrailId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ContextId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private Mock<IAIPromptService> _prompts = null!;
    private Mock<IAIProfileService> _profiles = null!;
    private Mock<IAIGuardrailService> _guardrails = null!;
    private Mock<IAIContextService> _contexts = null!;
    private AIPromptSerializer _serializer = null!;
    private SyncSerializerOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _prompts = new Mock<IAIPromptService>();
        _profiles = new Mock<IAIProfileService>();
        _guardrails = new Mock<IAIGuardrailService>();
        _contexts = new Mock<IAIContextService>();

        var aiService = new SyncAIService(
            Mock.Of<IAIConnectionService>(), _guardrails.Object, _contexts.Object, _profiles.Object,
            Mock.Of<IAISettingsService>(), new AIProviderCollection(() => []));

        _serializer = new AIPromptSerializer(
            NullLogger<SyncSerializerRoot<AIPrompt>>.Instance, new SyncAIPromptService(_prompts.Object), aiService);
        _options = new SyncSerializerOptions();
    }

    private void TargetHasEverything()
    {
        _profiles.Setup(x => x.GetProfileAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProfile { Alias = "default-chat", Name = "Default chat", ConnectionId = Guid.NewGuid() }.WithId(ProfileId));
        _guardrails.Setup(x => x.GetGuardrailAsync(GuardrailId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGuardrail { Alias = "no-pii", Name = "No PII" }.WithId(GuardrailId));
        _contexts.Setup(x => x.GetContextAsync(ContextId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIContext { Alias = "brand", Name = "Brand" }.WithId(ContextId));
    }

    private static AIPrompt Prompt() => new AIPrompt
    {
        Alias = "summarise",
        Name = "Summarise",
        Description = "Summarise the page",
        Instructions = "Summarise {{currentValue}} in <two> sentences & keep it plain.",
        ProfileId = ProfileId,
        ContextIds = [ContextId],
        GuardrailIds = [GuardrailId],
        Tags = ["seo", "content"],
        IsActive = false,
        IncludeEntityContext = false,
        OptionCount = 3,
        DisplayMode = AIPromptDisplayMode.TipTapTool,
        Scope = new AIPromptScope
        {
            AllowRules = [new AIPromptScopeRule { PropertyEditorUiAliases = ["Umb.PropertyEditorUi.TextArea"], ContentTypeAliases = ["article"] }],
            DenyRules = [new AIPromptScopeRule { PropertyAliases = ["internalNotes"] }],
        },
    }.WithId(PromptId);

    [Test]
    public async Task Prompt_RoundTrips()
    {
        TargetHasEverything();

        var xml = (await _serializer.SerializeAsync(Prompt(), _options)).Item!;
        Assert.That(xml.Element("Info")!.Element("Profile")!.Value, Is.EqualTo("default-chat"));

        // through text, as it would be on disk
        var result = await _serializer.DeserializeAsync(XElement.Parse(xml.ToString()), _options);

        Assert.That(result.Success, Is.True, result.Message);
        var item = result.Item!;
        Assert.Multiple(() =>
        {
            Assert.That(item.Id, Is.EqualTo(PromptId));
            Assert.That(item.Alias, Is.EqualTo("summarise"));
            Assert.That(item.Name, Is.EqualTo("Summarise"));
            Assert.That(item.Description, Is.EqualTo("Summarise the page"));
            Assert.That(item.Instructions, Is.EqualTo("Summarise {{currentValue}} in <two> sentences & keep it plain."));
            Assert.That(item.ProfileId, Is.EqualTo(ProfileId));
            Assert.That(item.ContextIds, Is.EqualTo(new[] { ContextId }));
            Assert.That(item.GuardrailIds, Is.EqualTo(new[] { GuardrailId }));
            Assert.That(item.Tags, Is.EqualTo(new[] { "content", "seo" }));
            Assert.That(item.IsActive, Is.False);
            Assert.That(item.IncludeEntityContext, Is.False);
            Assert.That(item.OptionCount, Is.EqualTo(3));
            Assert.That(item.DisplayMode, Is.EqualTo(AIPromptDisplayMode.TipTapTool));
            Assert.That(item.Scope!.AllowRules.Single().PropertyEditorUiAliases, Is.EqualTo(new[] { "Umb.PropertyEditorUi.TextArea" }));
            Assert.That(item.Scope.AllowRules.Single().ContentTypeAliases, Is.EqualTo(new[] { "article" }));
            Assert.That(item.Scope.AllowRules.Single().PropertyAliases, Is.Null);
            Assert.That(item.Scope.DenyRules.Single().PropertyAliases, Is.EqualTo(new[] { "internalNotes" }));
            Assert.That(result.Details, Is.Empty);
        });
        _prompts.Verify(x => x.SavePromptAsync(item, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Prompt_WithNothingOptional_RoundTrips()
    {
        var minimal = new AIPrompt { Alias = "plain", Name = "Plain", Instructions = "Do it." }.WithId(PromptId);

        var xml = (await _serializer.SerializeAsync(minimal, _options)).Item!;
        var result = await _serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Item!.ProfileId, Is.Null);
            Assert.That(result.Item.Description, Is.Null);
            Assert.That(result.Item.Scope, Is.Null);
            Assert.That(result.Item.ContextIds, Is.Empty);
            Assert.That(result.Details, Is.Empty);
        });
    }

    [Test]
    public async Task Prompt_CreatedByImport_GetsADateCreated()
    {
        var xml = (await _serializer.SerializeAsync(Prompt(), _options)).Item!;

        var before = DateTime.UtcNow;
        var result = await _serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Item!.DateCreated, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task Prompt_MissingReferences_AreLeftOffWithAWarningEach()
    {
        TargetHasEverything();
        var xml = (await _serializer.SerializeAsync(Prompt(), _options)).Item!;

        // a target that has none of them
        _profiles.Reset();
        _guardrails.Reset();
        _contexts.Reset();

        var result = await _serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Item!.ProfileId, Is.Null);
            Assert.That(result.Item.ContextIds, Is.Empty);
            Assert.That(result.Item.GuardrailIds, Is.Empty);
            Assert.That(result.Details!.Count(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Prompt_ResolvesReferencesByAlias_WhenKeysDifferOnTheTarget()
    {
        TargetHasEverything();
        var xml = (await _serializer.SerializeAsync(Prompt(), _options)).Item!;

        var targetProfileId = Guid.NewGuid();
        _profiles.Reset();
        _profiles.Setup(x => x.GetProfileByAliasAsync("default-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProfile { Alias = "default-chat", Name = "Default chat", ConnectionId = Guid.NewGuid() }.WithId(targetProfileId));

        var result = await _serializer.DeserializeAsync(xml, _options);

        Assert.That(result.Item!.ProfileId, Is.EqualTo(targetProfileId));
    }
}

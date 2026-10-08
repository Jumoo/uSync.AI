using Jumoo.Processing.Core.Pipelines;
using Jumoo.Processing.Core.Pipelines.Models;
using Jumoo.Processing.Core.Processing.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using uSync.AI.Complete.Security;
using uSync.AI.Complete.Services;
using uSync.AI.Complete.Tools;
using uSync.AI.Tools.Security;
using uSync.AI.Tools.Services;
using uSync.Publisher.Configuration;
using uSync.Publisher.Models;
using uSync.Publisher.Process.Models;
using uSync.Publisher.Strategies.Models;

namespace uSync.AI.Tests.Complete;

[TestFixture]
public class uSyncCompleteToolTests
{
    private static readonly Guid Editors = Constants.Security.EditorGroupKey;
    private static readonly Guid Writers = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid ItemKey = Guid.Parse("12121212-1212-1212-1212-121212121212");

    private static IUser User(Guid groupKey, params string[] permissions)
    {
        var group = Mock.Of<IReadOnlyUserGroup>(g => g.Key == groupKey && g.Permissions == new HashSet<string>(permissions));
        return Mock.Of<IUser>(u => u.Groups == new[] { group } && u.Username == "tester");
    }

    private static SyncPublishServer Server(string alias, bool push = true, bool pull = true, Guid[]? groups = null) => new()
    {
        Alias = alias,
        Name = alias.ToUpperInvariant(),
        Url = $"https://{alias}.example.com/umbraco",
        Icon = "icon-globe",
        PushEnabled = push,
        PullEnabled = pull,
        SendSettings = new uSyncSendSettings { UserGroups = groups ?? [] },
    };

    /// <summary>The uSync tool authorizer, answering with a fixed user or a refusal.</summary>
    private static IuSyncToolAuthorizer ToolAccess(IUser? user, uSyncToolAccess? refuse = null)
    {
        var mock = new Mock<IuSyncToolAuthorizer>();
        mock.Setup(x => x.Authorize(It.IsAny<uSyncToolAccess>())).Returns((uSyncToolAccess access) =>
            user is null || access == refuse
                ? uSyncToolAuthorization.Denied("no uSync access", user)
                : uSyncToolAuthorization.Allowed(user));
        return mock.Object;
    }

    private static uSyncCompleteToolAuthorizer Authorizer(IUser? user, params SyncPublishServer[] servers)
        => new(ToolAccess(user), Mock.Of<IuSyncCompleteServerSource>(s => s.GetServers() == servers));

    private static readonly string Push = uSyncCompleteToolAuthorizer.PushPermission;
    private static readonly string Pull = uSyncCompleteToolAuthorizer.PullPermission;

    // ---- who may push or pull, and where

    [Test]
    public void WithoutuSyncAccess_NothingIsAllowed()
    {
        var authorizer = Authorizer(null, Server("live"));

        Assert.Multiple(() =>
        {
            Assert.That(authorizer.AuthorizePublish(PublishMode.Push, "live").IsAuthorized, Is.False);
            Assert.That(authorizer.GetServers(PublishMode.Push).Servers, Is.Empty);
            Assert.That(authorizer.AuthorizeRestorePoint().IsAuthorized, Is.False);
        });
    }

    [Test]
    public void WithoutThePushPermission_PushIsRefused_EvenWithuSyncAccess()
    {
        var authorizer = Authorizer(User(Editors, Pull), Server("live"));

        var result = authorizer.AuthorizePublish(PublishMode.Push, "live");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAuthorized, Is.False);
            Assert.That(result.Message, Does.Contain("permission to push"));
        });
    }

    [Test]
    public void PushAndPull_NeedTheirOwnPermission()
    {
        var pusher = Authorizer(User(Editors, Push), Server("live"));

        Assert.Multiple(() =>
        {
            Assert.That(pusher.AuthorizePublish(PublishMode.Push, "live").IsAuthorized, Is.True);
            Assert.That(pusher.AuthorizePublish(PublishMode.Pull, "live").IsAuthorized, Is.False);
        });
    }

    [Test]
    public void Pull_AsksForImportAccess_AndPushOnlyForExportAccess()
    {
        // a user the uSync tools would let export but not import (a non-admin)
        var user = User(Editors, Push, Pull);
        var authorizer = new uSyncCompleteToolAuthorizer(
            ToolAccess(user, refuse: uSyncToolAccess.Import),
            Mock.Of<IuSyncCompleteServerSource>(s => s.GetServers() == new[] { Server("live") }));

        Assert.Multiple(() =>
        {
            Assert.That(authorizer.AuthorizePublish(PublishMode.Push, "live").IsAuthorized, Is.True);
            Assert.That(authorizer.AuthorizePublish(PublishMode.Pull, "live").IsAuthorized, Is.False);
        });
    }

    [Test]
    public void Servers_AreLimitedToTheDirectionTheyAllow()
    {
        var authorizer = Authorizer(User(Editors, Push, Pull), Server("push-only", pull: false), Server("pull-only", push: false));

        Assert.Multiple(() =>
        {
            Assert.That(authorizer.GetServers(PublishMode.Push).Servers.Select(x => x.Alias), Is.EqualTo(new[] { "push-only" }));
            Assert.That(authorizer.GetServers(PublishMode.Pull).Servers.Select(x => x.Alias), Is.EqualTo(new[] { "pull-only" }));
            Assert.That(authorizer.AuthorizePublish(PublishMode.Push, "pull-only").IsAuthorized, Is.False);
        });
    }

    [Test]
    public void Servers_AreLimitedToTheUsersGroups()
    {
        var authorizer = Authorizer(User(Writers, Push),
            Server("editors-only", groups: [Editors]), Server("writers", groups: [Writers]), Server("anyone"));

        Assert.Multiple(() =>
        {
            Assert.That(authorizer.GetServers(PublishMode.Push).Servers.Select(x => x.Alias), Is.EqualTo(new[] { "writers", "anyone" }));
            Assert.That(authorizer.AuthorizePublish(PublishMode.Push, "editors-only").IsAuthorized, Is.False);
        });
    }

    [Test]
    public void AServerThatIsHidden_AndOneThatDoesNotExist_GetTheSameAnswer()
    {
        var authorizer = Authorizer(User(Writers, Push), Server("editors-only", groups: [Editors]));

        var hidden = authorizer.AuthorizePublish(PublishMode.Push, "editors-only").Message!;
        var missing = authorizer.AuthorizePublish(PublishMode.Push, "nope").Message!;

        Assert.That(hidden.Replace("editors-only", "X"), Is.EqualTo(missing.Replace("nope", "X")));
    }

    [Test]
    public void ServerAlias_IsMatchedWhateverItsCasing()
    {
        var result = Authorizer(User(Editors, Push), Server("live")).AuthorizePublish(PublishMode.Push, "LIVE");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAuthorized, Is.True);
            Assert.That(result.Server!.Alias, Is.EqualTo("live"));
        });
    }

    // ---- the tools

    private Mock<IuSyncCompletePipelineRunner> _runner = null!;
    private PublisherProcessingOptions? _sent;
    private string? _strategy;

    private Mock<IuSyncCompletePipelineRunner> Runner()
    {
        _runner = new Mock<IuSyncCompletePipelineRunner>();
        _runner.Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProcessingOptions>(),
                It.IsAny<IUser>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, string strategy, IProcessingOptions options, IUser _, string? _, CancellationToken _) =>
            {
                _strategy = strategy;
                _sent = options as PublisherProcessingOptions;
            })
            .ReturnsAsync((string operation, string _, string _, IProcessingOptions _, IUser _, string? server, CancellationToken _) =>
                new uSyncCompleteToolResult(operation, true, "Completed", server));
        return _runner;
    }

    [Test]
    public async Task Publish_SendsTheOneItemToTheServer_AsTheActingUser()
    {
        var user = User(Editors, Push);
        IAITool tool = new uSyncPublishToServerTool(Authorizer(user, Server("live")), Runner().Object);

        var result = (uSyncCompleteToolResult)await tool.ExecuteAsync(new uSyncPublishArgs("LIVE", ItemKey, IncludeDependencies: true));

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(_strategy, Is.EqualTo("RealtimePushStrategy"));
            Assert.That(_sent!.Mode, Is.EqualTo(PublishMode.Push));
            Assert.That(_sent.Server, Is.EqualTo("live"));
            Assert.That(_sent.Items!.Single().Udi, Is.EqualTo(new GuidUdi(Constants.UdiEntityType.Document, ItemKey)));
            Assert.That(_sent.PublisherOptions!.IncludeChildren, Is.False);
            Assert.That(_sent.PublisherOptions.IncludeMedia, Is.True);
            Assert.That(_sent.PublisherOptions.IncludeDependencies, Is.True);
        });
        _runner.Verify(x => x.RunAsync("push", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProcessingOptions>(),
            user, "live", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Pull_OfAMediaItem_UsesThePullStrategyAndTheMediaType()
    {
        IAITool tool = new uSyncPullFromServerTool(Authorizer(User(Editors, Pull), Server("live")), Runner().Object);

        await tool.ExecuteAsync(new uSyncPublishArgs("live", ItemKey, ItemType: "Media", IncludeChildren: true));

        Assert.Multiple(() =>
        {
            Assert.That(_strategy, Is.EqualTo("RealtimePullStrategy"));
            Assert.That(_sent!.Mode, Is.EqualTo(PublishMode.Pull));
            Assert.That(_sent.Items!.Single().Udi.EntityType, Is.EqualTo(Constants.UdiEntityType.Media));
            Assert.That(_sent.PublisherOptions!.IncludeChildren, Is.True);
        });
    }

    [Test]
    public async Task Publish_Refused_NeverStartsAPipeline()
    {
        IAITool tool = new uSyncPublishToServerTool(Authorizer(User(Editors, Pull), Server("live")), Runner().Object);

        var result = (uSyncCompleteToolResult)await tool.ExecuteAsync(new uSyncPublishArgs("live", ItemKey));

        Assert.That(result.Success, Is.False);
        _runner.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Publish_WithNoItemKey_IsRefused_RatherThanSendingTheWholeTree()
    {
        IAITool tool = new uSyncPublishToServerTool(Authorizer(User(Editors, Push), Server("live")), Runner().Object);

        var result = (uSyncCompleteToolResult)await tool.ExecuteAsync(new uSyncPublishArgs("live", Guid.Empty));

        Assert.That(result.Success, Is.False);
        _runner.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ListServers_ShowsOnlyWhatTheUserCanUse_AndNoUrls()
    {
        IAITool tool = new uSyncListServersTool(
            Authorizer(User(Editors, Push, Pull), Server("live", pull: false), Server("stage", push: false)));

        var push = (uSyncServerList)await tool.ExecuteAsync(new uSyncListServersArgs());
        var pull = (uSyncServerList)await tool.ExecuteAsync(new uSyncListServersArgs("pull"));

        Assert.Multiple(() =>
        {
            Assert.That(push.Servers.Select(x => x.Alias), Is.EqualTo(new[] { "live" }));
            Assert.That(pull.Servers.Select(x => x.Alias), Is.EqualTo(new[] { "stage" }));
            Assert.That(System.Text.Json.JsonSerializer.Serialize(push), Does.Not.Contain("example.com"));
        });
    }

    [Test]
    public async Task ListServers_WithoutPermission_SaysWhy()
    {
        IAITool tool = new uSyncListServersTool(Authorizer(User(Editors), Server("live")));

        var result = (uSyncServerList)await tool.ExecuteAsync(new uSyncListServersArgs());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Servers, Is.Empty);
            Assert.That(result.Message, Is.Not.Empty);
        });
    }

    [Test]
    public async Task RestorePoint_WithoutuSyncAccess_NeverStartsAPipeline()
    {
        IAITool tool = new uSyncCreateRestorePointTool(Authorizer(null), Runner().Object);

        var result = (uSyncCompleteToolResult)await tool.ExecuteAsync(new uSyncRestorePointArgs("before the import"));

        Assert.That(result.Success, Is.False);
        _runner.VerifyNoOtherCalls();
    }

    [Test]
    public void EveryToolThatChangesSomething_NeedsApproval()
    {
        var authorizer = Authorizer(User(Editors, Push, Pull));
        var runner = Runner().Object;
        IAITool list = new uSyncListServersTool(authorizer);
        IAITool push = new uSyncPublishToServerTool(authorizer, runner);
        IAITool pull = new uSyncPullFromServerTool(authorizer, runner);
        IAITool restore = new uSyncCreateRestorePointTool(authorizer, runner);

        Assert.Multiple(() =>
        {
            Assert.That(list.RequiresApproval, Is.False);
            Assert.That(list.ScopeId, Is.EqualTo(uSyncPublisherReadScope.ScopeId));

            Assert.That(push.IsDestructive && push.RequiresApproval, Is.True);
            Assert.That(pull.IsDestructive && pull.RequiresApproval, Is.True);
            Assert.That(restore.IsDestructive, Is.False);
            Assert.That(restore.RequiresApproval, Is.True);
            Assert.That(new[] { push.ScopeId, pull.ScopeId, restore.ScopeId }, Is.All.EqualTo(uSyncPublisherWriteScope.ScopeId));
            Assert.That(new uSyncPublisherWriteScope().IsDestructive, Is.True);
        });
    }

    [Test]
    public async Task ApprovalPrompt_NamesTheServerAndWhatGoesWithTheItem()
    {
        IAITool push = new uSyncPublishToServerTool(Authorizer(User(Editors, Push)), Runner().Object);

        var text = await push.DescribeInvocationAsync(new uSyncPublishArgs("live", ItemKey, IncludeChildren: true, IncludeMedia: false));

        Assert.That(text, Does.Contain("'live'").And.Contain(ItemKey.ToString()).And.Contain("its descendants").And.Not.Contain("media it uses"));
    }

    // ---- the pipeline runner

    [Test]
    public async Task Runner_WhileAnotherOperationIsRunning_IsRefused()
    {
        var pipelines = new Mock<IPipelineService>();
        var gate = new uSyncOperationGate();
        var runner = new uSyncCompletePipelineRunner(pipelines.Object, gate, NullLogger<uSyncCompletePipelineRunner>.Instance);

        using var running = gate.TryAcquire();
        var result = await runner.RunAsync("push", "p", "s", Mock.Of<IProcessingOptions>(), User(Editors, Push), "live", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("already running"));
        });
        pipelines.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Runner_ThatThrows_ReportsFailureWithoutTheExceptionText_AndReleasesTheGate()
    {
        var user = User(Editors, Push);
        var pipelines = new Mock<IPipelineService>();
        pipelines.Setup(x => x.CreatePipeline(It.IsAny<CreatePipelineOptions>()))
            .ThrowsAsync(new InvalidOperationException("AppKey=hunter2 rejected by https://live.example.com"));
        var gate = new uSyncOperationGate();
        var runner = new uSyncCompletePipelineRunner(pipelines.Object, gate, NullLogger<uSyncCompletePipelineRunner>.Instance);

        var result = await runner.RunAsync("push", "p", "s", Mock.Of<IProcessingOptions>(), user, "live", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Not.Contain("hunter2"));
            Assert.That(gate.TryAcquire(), Is.Not.Null);
        });
        pipelines.Verify(x => x.CreatePipeline(It.Is<CreatePipelineOptions>(o => o.User == user)), Times.Once);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using uSync.AI.Tools;
using uSync.AI.Tools.Models;
using uSync.AI.Tools.Security;
using uSync.AI.Tools.Services;
using uSync.AI.Tools.Tools;
using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Models;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers.Models;

namespace uSync.AI.Tests.Tools;

[TestFixture]
public class uSyncToolTests
{
    private static IOptionsMonitor<uSyncAIToolsOptions> Options(uSyncAIToolsOptions? options = null)
        => Mock.Of<IOptionsMonitor<uSyncAIToolsOptions>>(x => x.CurrentValue == (options ?? new uSyncAIToolsOptions()));

    private static IUser User(string[] sections, bool admin = false)
    {
        var groups = admin
            ? new[] { Mock.Of<IReadOnlyUserGroup>(g => g.Alias == Umbraco.Cms.Core.Constants.Security.AdminGroupAlias) }
            : new[] { Mock.Of<IReadOnlyUserGroup>(g => g.Alias == "editor") };

        return Mock.Of<IUser>(u => u.AllowedSections == sections && u.Groups == groups && u.Username == "tester");
    }

    private static uSyncToolAuthorizer Authorizer(IUser? user, uSyncAIToolsOptions? options = null)
    {
        var security = user is null ? null : Mock.Of<IBackOfficeSecurity>(s => s.CurrentUser == user);
        return new uSyncToolAuthorizer(Mock.Of<IBackOfficeSecurityAccessor>(a => a.BackOfficeSecurity == security), Options(options));
    }

    // ---- who may run what

    [TestCase(uSyncToolAccess.Read)]
    [TestCase(uSyncToolAccess.Export)]
    [TestCase(uSyncToolAccess.Import)]
    public void NoUser_IsRefusedEverything(uSyncToolAccess access)
        => Assert.That(Authorizer(null).Authorize(access).IsAuthorized, Is.False);

    [TestCase(uSyncToolAccess.Read)]
    [TestCase(uSyncToolAccess.Export)]
    [TestCase(uSyncToolAccess.Import)]
    public void UserWithoutSettingsAccess_IsRefusedEverything(uSyncToolAccess access)
        => Assert.That(Authorizer(User(["content", "media"], admin: true)).Authorize(access).IsAuthorized, Is.False);

    [TestCase(uSyncToolAccess.Read, true)]
    [TestCase(uSyncToolAccess.Export, true)]
    [TestCase(uSyncToolAccess.Import, false)]
    public void NonAdminWithSettingsAccess_CanReadAndExportButNotImport(uSyncToolAccess access, bool expected)
        => Assert.That(Authorizer(User(["content", "settings"])).Authorize(access).IsAuthorized, Is.EqualTo(expected));

    [Test]
    public void UserWithOnlyTheuSyncSection_HasAccess()
        => Assert.That(Authorizer(User([uSyncConstants.uSyncSection])).Authorize(uSyncToolAccess.Read).IsAuthorized, Is.True);

    [TestCase(uSyncToolAccess.Read)]
    [TestCase(uSyncToolAccess.Export)]
    [TestCase(uSyncToolAccess.Import)]
    public void AdminWithSettingsAccess_CanDoEverything(uSyncToolAccess access)
        => Assert.That(Authorizer(User(["settings"], admin: true)).Authorize(access).IsAuthorized, Is.True);

    [Test]
    public void NonAdmin_CanImport_WhenTheAdminRequirementIsTurnedOff()
    {
        var authorizer = Authorizer(User(["settings"]), new uSyncAIToolsOptions { RequireAdminForImport = false });

        Assert.That(authorizer.Authorize(uSyncToolAccess.Import).IsAuthorized, Is.True);
    }

    [Test]
    public void EachActionAsksForTheRightAccess()
        => Assert.Multiple(() =>
        {
            Assert.That(uSyncToolRunner.AccessFor(HandlerActions.Report), Is.EqualTo(uSyncToolAccess.Read));
            Assert.That(uSyncToolRunner.AccessFor(HandlerActions.Export), Is.EqualTo(uSyncToolAccess.Export));
            Assert.That(uSyncToolRunner.AccessFor(HandlerActions.Import), Is.EqualTo(uSyncToolAccess.Import));
        });

    // ---- the runner

    private Mock<ISyncActionService> _actions = null!;
    private uSyncOperationGate _gate = null!;

    private uSyncToolRunner Runner(IUser? user)
    {
        _actions = new Mock<ISyncActionService>();
        _actions.Setup(x => x.GetActionHandlers(It.IsAny<HandlerActions>(), It.IsAny<uSyncOptions?>())).Returns(
        [
            new SyncHandlerView { Alias = "dataTypeHandler", Name = "Data types", Group = "Settings", Enabled = true },
            new SyncHandlerView { Alias = "contentHandler", Name = "Content", Group = "Content", Enabled = true },
        ]);

        var empty = new SyncActionResult();
        _actions.Setup(x => x.ReportHandlerAsync(It.IsAny<SyncActionOptions>(), It.IsAny<uSyncCallbacks?>())).ReturnsAsync(empty);
        _actions.Setup(x => x.ExportHandlerAsync(It.IsAny<SyncActionOptions>(), It.IsAny<uSyncCallbacks?>())).ReturnsAsync(empty);
        _actions.Setup(x => x.ImportHandlerAsync(It.IsAny<SyncActionOptions>(), It.IsAny<uSyncCallbacks?>())).ReturnsAsync(empty);
        _actions.Setup(x => x.ImportPostAsync(It.IsAny<SyncFinalActionRequest>())).ReturnsAsync(empty);
        _actions.Setup(x => x.FinishProcessAsync(It.IsAny<SyncFinalActionRequest>())).ReturnsAsync(empty);

        var config = new Mock<ISyncConfigService>();
        config.SetupGet(x => x.Settings).Returns(new uSyncSettings());
        config.Setup(x => x.GetFolders()).Returns(["uSync/v17"]);

        _gate = new uSyncOperationGate();
        return new uSyncToolRunner(_actions.Object, config.Object, Authorizer(user), _gate, Options(), NullLogger<uSyncToolRunner>.Instance);
    }

    [Test]
    public async Task Import_ByANonAdmin_IsRefused_AndNeverTouchesuSync()
    {
        var runner = Runner(User(["settings"]));

        var result = await runner.RunAsync(new uSyncToolRunRequest(HandlerActions.Import), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("administrators"));
        });
        _actions.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Report_WithNoUser_IsRefused_AndNeverTouchesuSync()
    {
        var runner = Runner(null);

        var result = await runner.RunAsync(new uSyncToolRunRequest(HandlerActions.Report), CancellationToken.None);

        Assert.That(result.Success, Is.False);
        _actions.VerifyNoOtherCalls();
    }

    [Test]
    public void ListHandlers_WithoutAccess_ReturnsNothing()
    {
        var runner = Runner(User(["content"]));

        var result = runner.ListHandlers(HandlerActions.Report, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Handlers, Is.Empty);
        });
        _actions.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Import_ByAnAdmin_RunsEveryHandlerThenPostAndFinish()
    {
        var runner = Runner(User(["settings"], admin: true));

        var result = await runner.RunAsync(new uSyncToolRunRequest(HandlerActions.Import, Force: true), CancellationToken.None);

        Assert.That(result.Success, Is.True, result.Message);
        _actions.Verify(x => x.StartProcessAsync(It.Is<SyncStartActionRequest>(r => r.Username == "tester")), Times.Once);
        _actions.Verify(x => x.ImportHandlerAsync(It.Is<SyncActionOptions>(o => o.Handler == "dataTypeHandler" && o.Force), null), Times.Once);
        _actions.Verify(x => x.ImportHandlerAsync(It.Is<SyncActionOptions>(o => o.Handler == "contentHandler" && o.Force), null), Times.Once);
        _actions.Verify(x => x.ImportPostAsync(It.IsAny<SyncFinalActionRequest>()), Times.Once);
        _actions.Verify(x => x.FinishProcessAsync(It.IsAny<SyncFinalActionRequest>()), Times.Once);
    }

    [Test]
    public async Task Report_LimitedToNamedHandlers_RunsOnlyThose()
    {
        var runner = Runner(User(["settings"]));

        await runner.RunAsync(new uSyncToolRunRequest(HandlerActions.Report, Handlers: ["CONTENTHANDLER"]), CancellationToken.None);

        _actions.Verify(x => x.ReportHandlerAsync(It.Is<SyncActionOptions>(o => o.Handler == "contentHandler"), null), Times.Once);
        _actions.Verify(x => x.ReportHandlerAsync(It.Is<SyncActionOptions>(o => o.Handler == "dataTypeHandler"), null), Times.Never);
        _actions.Verify(x => x.ImportPostAsync(It.IsAny<SyncFinalActionRequest>()), Times.Never);
    }

    [Test]
    public async Task Run_WithNoMatchingHandlers_FailsBeforeStarting()
    {
        var runner = Runner(User(["settings"]));

        var result = await runner.RunAsync(new uSyncToolRunRequest(HandlerActions.Report, Handlers: ["nope"]), CancellationToken.None);

        Assert.That(result.Success, Is.False);
        _actions.Verify(x => x.StartProcessAsync(It.IsAny<SyncStartActionRequest>()), Times.Never);
    }

    [Test]
    public async Task Run_WhileAnotherIsRunning_IsRefused_ThenWorksOnceItFinishes()
    {
        var runner = Runner(User(["settings"]));
        var request = new uSyncToolRunRequest(HandlerActions.Report);

        var running = _gate.TryAcquire();
        var blocked = await runner.RunAsync(request, CancellationToken.None);
        running!.Dispose();
        var after = await runner.RunAsync(request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.Message, Does.Contain("already running"));
            Assert.That(after.Success, Is.True);
        });
    }

    [Test]
    public async Task Run_ThatThrows_ReportsFailureWithoutTheExceptionText_AndReleasesTheGate()
    {
        var runner = Runner(User(["settings"]));
        _actions.Setup(x => x.ReportHandlerAsync(It.IsAny<SyncActionOptions>(), It.IsAny<uSyncCallbacks?>()))
            .ThrowsAsync(new InvalidOperationException("connection string Server=secret;Password=hunter2"));

        var result = await runner.RunAsync(new uSyncToolRunRequest(HandlerActions.Report), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Not.Contain("hunter2"));
            Assert.That(_gate.TryAcquire(), Is.Not.Null);
        });
    }

    // ---- what the model is told

    [Test]
    public void Result_ListsOnlyChangedOrFailedItems_AndSaysWhenTheListIsCut()
    {
        var actions = new List<uSyncAction>
        {
            uSyncAction.SetAction(true, "Same", "DataType", Core.ChangeType.NoChange),
            uSyncAction.SetAction(true, "Updated", "DataType", Core.ChangeType.Update),
            uSyncAction.SetAction(true, "Created", "DataType", Core.ChangeType.Create),
            uSyncAction.Fail("Broken", "dataTypeHandler", "DataType", Core.ChangeType.Fail, "it broke", new Exception("it broke")),
        };

        var result = uSyncToolResult.From("report", actions, maxChanges: 2);

        Assert.Multiple(() =>
        {
            Assert.That(result.ItemCount, Is.EqualTo(4));
            Assert.That(result.Changes.Select(x => x.Name), Is.EqualTo(new[] { "Updated", "Created" }));
            Assert.That(result.ChangesTruncated, Is.True);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCount, Is.EqualTo(1));
        });
    }

    // ---- how the tools are declared

    [Test]
    public void WriteTools_AreDestructive_SoTheyNeedApproval_AndReadToolsAreNot()
    {
        var runner = Runner(User(["settings"]));
        IAITool list = new uSyncListHandlersTool(runner);
        IAITool report = new uSyncReportTool(runner);
        IAITool export = new uSyncExportTool(runner);
        IAITool import = new uSyncImportTool(runner);

        Assert.Multiple(() =>
        {
            Assert.That(list.RequiresApproval, Is.False);
            Assert.That(report.RequiresApproval, Is.False);
            Assert.That(list.ScopeId, Is.EqualTo(uSyncReadScope.ScopeId));
            Assert.That(report.ScopeId, Is.EqualTo(uSyncReadScope.ScopeId));

            Assert.That(export.RequiresApproval, Is.True);
            Assert.That(import.RequiresApproval, Is.True);
            Assert.That(export.ScopeId, Is.EqualTo(uSyncWriteScope.ScopeId));
            Assert.That(import.ScopeId, Is.EqualTo(uSyncWriteScope.ScopeId));

            Assert.That(new uSyncWriteScope().IsDestructive, Is.True);
            Assert.That(new uSyncReadScope().IsDestructive, Is.False);
        });
    }

    [Test]
    public async Task ApprovalPrompt_SaysWhatWillRun()
    {
        IAITool import = new uSyncImportTool(Runner(User(["settings"], admin: true)));

        var everything = await import.DescribeInvocationAsync(new uSyncImportArgs());
        var settings = await import.DescribeInvocationAsync(new uSyncImportArgs(Group: "Settings", Force: true));

        Assert.Multiple(() =>
        {
            Assert.That(everything, Does.Contain("everything"));
            Assert.That(settings, Does.Contain("the Settings group").And.Contain("forced"));
        });
    }
}

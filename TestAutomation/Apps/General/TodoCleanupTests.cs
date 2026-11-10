using Automation.apps.General.Household;
using Automation.Configuration;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class TodoCleanupTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private void Start(params string[] lists)
    {
        _ctx.HaContext.GetAllEntities().Returns([.. lists.Select(l => new Entity(_ctx.HaContext, l))]);
        // 2026-11-05 22:00 UTC is 23:00 Dutch time, an hour before midnight
        _ctx.SetCurrentTime(new DateTime(2026, 11, 5, 22, 0, 0, DateTimeKind.Utc));
        _ctx.InitApp<TodoCleanup>(Options.Create(new AppConfig()));
    }

    [Fact]
    public void BeforeMidnight_NothingIsRemoved()
    {
        Start("todo.vincent", "todo.boodschappen");

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(59).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.DidNotReceive().CallService("todo", "remove_completed_items", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Fact]
    public void AtMidnight_TheCompletedItemsOfEveryListAreRemoved()
    {
        Start("todo.vincent", "todo.carleen", "todo.dagelijks", "todo.boodschappen");

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(61).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        foreach (var list in new[] { "vincent", "carleen", "dagelijks", "boodschappen" })
            _ctx.VerifyCallService("todo", "remove_completed_items", list, times: 1);
    }
}

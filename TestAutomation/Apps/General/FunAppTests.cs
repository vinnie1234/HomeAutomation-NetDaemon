using Automation.apps.General;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class FunAppTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();
    
    [Fact]
    public void ShouldEnableLightsHallWhenStartFriends()
    {
        _ctx.InitApp<FunApp>();
        _ctx.ChangeStateFor("input_button.start_friends")
            .FromState("off")
            .ToState("on");
        
        _ctx.VerifyCallService("light","turn_on", "hal", 1);
        _ctx.VerifyCallService("switch","turn_on", "bot_29ff", 1);
    }
}
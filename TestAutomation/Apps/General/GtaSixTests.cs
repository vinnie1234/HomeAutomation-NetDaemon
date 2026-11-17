using System.Text.Json;
using Automation.apps.General.Fun;
using Xunit;

namespace TestAutomation.Apps.General;

public class GtaSixTests
{
    private static JsonElement? Attributes(string json) => JsonDocument.Parse(json).RootElement;

    [Theory]
    [InlineData("{\"supported_color_modes\":[\"xy\",\"color_temp\"]}", true)]
    [InlineData("{\"supported_color_modes\":[\"hs\"]}", true)]
    [InlineData("{\"supported_color_modes\":[\"color_temp\"]}", false)]
    [InlineData("{\"supported_color_modes\":[\"brightness\"]}", false)]
    [InlineData("{\"supported_color_modes\":[\"xy\"],\"entity_id\":[\"light.a\",\"light.b\"]}", false)]
    [InlineData("{}", false)]
    public void OnlyColourLightsThatAreNotGroupsGetViceCityColours(string json, bool expected) =>
        Assert.Equal(expected, GtaSix.IsColorLight(Attributes(json)));
}

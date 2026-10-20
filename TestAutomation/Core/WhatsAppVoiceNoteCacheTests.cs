using Automation.Core;
using FluentAssertions;
using Xunit;

namespace TestAutomation.Core;

public class WhatsAppVoiceNoteCacheTests
{
    [Fact]
    public void Add_ThenTryGet_ReturnsTheSameBytes()
    {
        var cache = new WhatsAppVoiceNoteCache();
        var bytes = "some ogg opus bytes"u8.ToArray();

        var id = cache.Add(bytes);

        cache.TryGet(id).Should().Equal(bytes);
    }

    [Fact]
    public void TryGet_WithAnUnknownId_ReturnsNull()
    {
        var cache = new WhatsAppVoiceNoteCache();

        cache.TryGet(Guid.NewGuid().ToString("N")).Should().BeNull();
    }

    [Fact]
    public void Add_GivesEachVoiceNoteADifferentUnguessableId()
    {
        var cache = new WhatsAppVoiceNoteCache();

        var id1 = cache.Add("a"u8.ToArray());
        var id2 = cache.Add("b"u8.ToArray());

        id1.Should().NotBe(id2);
    }
}

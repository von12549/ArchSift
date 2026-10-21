using System.Text;
using System.Text.Json;
using ArchSift.TestGate;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class DiscoveryOutputTests
{
    [Fact]
    public void Utf8UnicodeAndIdsRemainExact()
    {
        const string json = "{\"tests\":[{\"uid\":\"stable-id\",\"displayName\":\"雪 ···\"}]}";
        var decoded = DiscoveryOutput.Decode(Encoding.UTF8.GetBytes(json), 437);
        Assert.Equal(json, decoded.Text);
        Assert.Equal("utf-8", decoded.Encoding);
    }

    [Fact]
    public void ActualWindowsOemDotsAreDecodedWithoutChangingUid()
    {
        var prefix = Encoding.ASCII.GetBytes("{\"tests\":[{\"uid\":\"stable-id\",\"displayName\":\"name");
        var bytes = prefix.Concat(new byte[] { 0xfa, 0xfa, 0xfa }).Concat(Encoding.ASCII.GetBytes("\"}]}")).ToArray();
        var decoded = DiscoveryOutput.Decode(bytes, 437);
        using var json = JsonDocument.Parse(decoded.Text);
        Assert.Equal("stable-id", json.RootElement.GetProperty("tests")[0].GetProperty("uid").GetString());
        Assert.Equal("name···", json.RootElement.GetProperty("tests")[0].GetProperty("displayName").GetString());
        Assert.DoesNotContain('\uFFFD', decoded.Text);
        Assert.Equal("ibm437", decoded.Encoding);
        Assert.Throws<DecoderFallbackException>(() => DiscoveryOutput.Decode(bytes, null));
        Assert.ThrowsAny<JsonException>(() => DiscoveryOutput.Decode(bytes[..^1], 437));
    }
}

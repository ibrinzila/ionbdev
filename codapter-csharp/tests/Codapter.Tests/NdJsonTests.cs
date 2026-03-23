using Codapter.Core.Utilities;
using Xunit;

namespace Codapter.Tests;

public class NdJsonTests
{
    [Fact]
    public void Serialize_AppendsNewline()
    {
        var result = NdJson.Serialize(new { hello = "world" });
        Assert.EndsWith("\n", result);
    }

    [Fact]
    public void Deserialize_ParsesValidJson()
    {
        var result = NdJson.Deserialize<Dictionary<string, string>>("{\"hello\":\"world\"}\n");
        Assert.NotNull(result);
        Assert.Equal("world", result!["hello"]);
    }

    [Fact]
    public void Deserialize_ReturnsNullForEmpty()
    {
        var result = NdJson.Deserialize<Dictionary<string, string>>("");
        Assert.Null(result);
    }

    [Fact]
    public void ParseLine_HandlesCarriageReturn()
    {
        var result = NdJson.ParseLine("{\"key\":\"value\"}\r\n");
        Assert.NotNull(result);
        Assert.Equal("value", result!.Value.GetProperty("key").GetString());
    }

    [Fact]
    public void SerializeLine_ProducesNdjsonFormat()
    {
        var line = NdJson.SerializeLine(new { a = 1 });
        Assert.EndsWith("\n", line);
        Assert.Contains("\"a\":1", line);
    }
}

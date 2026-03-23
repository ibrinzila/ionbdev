using System.Text.Json;
using Codapter.Core.Models;
using Xunit;

namespace Codapter.Tests;

public class JsonRpcTests
{
    [Fact]
    public void JsonRpcId_StringEquality()
    {
        JsonRpcId a = "test-id";
        JsonRpcId b = "test-id";
        Assert.Equal(a, b);
    }

    [Fact]
    public void JsonRpcId_NumberEquality()
    {
        JsonRpcId a = 42L;
        JsonRpcId b = 42L;
        Assert.Equal(a, b);
    }

    [Fact]
    public void JsonRpcId_SerializesString()
    {
        var id = new JsonRpcId("req-1");
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("\"req-1\"", json);
    }

    [Fact]
    public void JsonRpcId_SerializesNumber()
    {
        var id = new JsonRpcId(42);
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("42", json);
    }

    [Fact]
    public void Success_CreatesValidResponse()
    {
        var response = JsonRpcHelpers.Success(new JsonRpcId("1"), new { ok = true });

        Assert.Equal(new JsonRpcId("1"), response.Id);
        Assert.Equal(JsonValueKind.Object, response.Result.ValueKind);
    }

    [Fact]
    public void Failure_CreatesErrorResponse()
    {
        var response = JsonRpcHelpers.Failure(new JsonRpcId("1"), -32601, "Method not found");

        Assert.Equal(-32601, response.Error.Code);
        Assert.Equal("Method not found", response.Error.Message);
    }

    [Fact]
    public void IsRequest_DetectsValidRequest()
    {
        var json = JsonSerializer.SerializeToElement(new { id = 1, method = "test" });
        Assert.True(JsonRpcHelpers.IsRequest(json));
    }

    [Fact]
    public void IsNotification_DetectsValidNotification()
    {
        var json = JsonSerializer.SerializeToElement(new { method = "test" });
        Assert.True(JsonRpcHelpers.IsNotification(json));
    }
}

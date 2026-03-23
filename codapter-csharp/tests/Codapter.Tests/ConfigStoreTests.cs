using System.Text.Json;
using Codapter.Core.Models;
using Codapter.Core.Services;
using Xunit;

namespace Codapter.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempFile;
    private readonly InMemoryConfigStore _store;

    public ConfigStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"codapter-config-{Guid.NewGuid()}.json");
        _store = new InMemoryConfigStore(_tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void Read_ReturnsDefaultConfig()
    {
        var result = _store.Read();

        Assert.Equal(0, result.Version);
        Assert.NotNull(result.Config);
    }

    [Fact]
    public void WriteValue_UpdatesConfig()
    {
        var result = _store.WriteValue(0, new ConfigEdit
        {
            Key = "model",
            Value = JsonSerializer.SerializeToElement("gpt-4")
        });

        Assert.Equal(1, result.Version);
        Assert.Equal("gpt-4", result.Config.Model);
    }

    [Fact]
    public void WriteBatch_AppliesMultipleEdits()
    {
        var result = _store.WriteBatch(0, new List<ConfigEdit>
        {
            new() { Key = "model", Value = JsonSerializer.SerializeToElement("claude-3") },
            new() { Key = "web_search", Value = JsonSerializer.SerializeToElement(true) }
        });

        Assert.Equal(1, result.Version);
        Assert.Equal("claude-3", result.Config.Model);
        Assert.True(result.Config.WebSearch);
    }

    [Fact]
    public void WriteValue_VersionMismatch_Throws()
    {
        _store.WriteValue(0, new ConfigEdit
        {
            Key = "model",
            Value = JsonSerializer.SerializeToElement("test")
        });

        Assert.Throws<InvalidOperationException>(() =>
            _store.WriteValue(0, new ConfigEdit
            {
                Key = "model",
                Value = JsonSerializer.SerializeToElement("test2")
            }));
    }
}

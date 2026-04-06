using EdgeParse.Core.Api;
using EdgeParse.Core.Models;
using Xunit;

namespace EdgeParse.Tests;

public class ProcessingConfigTests
{
    [Fact]
    public void ParsePageSet_SinglePage_ReturnsSet()
    {
        var config = new ProcessingConfig { Pages = "3" };
        var set = config.ParsePageSet();

        Assert.NotNull(set);
        Assert.Single(set);
        Assert.Contains(3, set);
    }

    [Fact]
    public void ParsePageSet_Range_ReturnsAllPages()
    {
        var config = new ProcessingConfig { Pages = "1-5" };
        var set = config.ParsePageSet();

        Assert.NotNull(set);
        Assert.Equal(5, set!.Count);
        Assert.Contains(1, set);
        Assert.Contains(5, set);
    }

    [Fact]
    public void ParsePageSet_Mixed_ReturnsCorrectPages()
    {
        var config = new ProcessingConfig { Pages = "1,3,5-7" };
        var set = config.ParsePageSet();

        Assert.NotNull(set);
        Assert.Equal(5, set!.Count);
        Assert.Contains(1, set);
        Assert.Contains(3, set);
        Assert.Contains(5, set);
        Assert.Contains(6, set);
        Assert.Contains(7, set);
    }

    [Fact]
    public void ParsePageSet_Null_ReturnsNull()
    {
        var config = new ProcessingConfig { Pages = null };
        Assert.Null(config.ParsePageSet());
    }

    [Fact]
    public void ParsePageSet_Empty_ReturnsNull()
    {
        var config = new ProcessingConfig { Pages = "" };
        Assert.Null(config.ParsePageSet());
    }

    [Fact]
    public void DefaultConfig_HasCorrectDefaults()
    {
        var config = new ProcessingConfig();

        Assert.Equal(ReadingOrder.XyCut, config.ReadingOrder);
        Assert.Equal(TableMethod.Default, config.TableMethod);
        Assert.False(config.IncludeHeaderFooter);
        Assert.True(config.FilterConfig.FilterHiddenText);
        Assert.True(config.FilterConfig.FilterOutOfPage);
        Assert.True(config.FilterConfig.FilterTinyText);
        Assert.Equal(1.5, config.FilterConfig.ContrastRatioThreshold);
    }
}

using Codapter.Core.Services;
using Xunit;

namespace Codapter.Tests;

public class ToolItemsTests
{
    [Theory]
    [InlineData("bash", "commandExecution")]
    [InlineData("shell", "commandExecution")]
    [InlineData("exec", "commandExecution")]
    [InlineData("terminal", "commandExecution")]
    [InlineData("run", "commandExecution")]
    [InlineData("command", "commandExecution")]
    public void ClassifyToolName_CommandTools(string toolName, string expected)
    {
        Assert.Equal(expected, ToolItems.ClassifyToolName(toolName));
    }

    [Theory]
    [InlineData("edit", "fileChange")]
    [InlineData("write", "fileChange")]
    [InlineData("patch", "fileChange")]
    [InlineData("create_file", "fileChange")]
    [InlineData("file_edit", "fileChange")]
    public void ClassifyToolName_FileTools(string toolName, string expected)
    {
        Assert.Equal(expected, ToolItems.ClassifyToolName(toolName));
    }

    [Theory]
    [InlineData("think", "agentMessage")]
    [InlineData("analyze", "agentMessage")]
    [InlineData("search", "agentMessage")]
    [InlineData("unknown_tool", "agentMessage")]
    public void ClassifyToolName_DefaultsToAgentMessage(string toolName, string expected)
    {
        Assert.Equal(expected, ToolItems.ClassifyToolName(toolName));
    }

    [Theory]
    [InlineData("runCommand", "commandExecution")]
    [InlineData("fileEdit", "fileChange")]
    [InlineData("shell_exec", "commandExecution")]
    [InlineData("create_file", "fileChange")]
    public void ClassifyToolName_TokenizedMatching(string toolName, string expected)
    {
        Assert.Equal(expected, ToolItems.ClassifyToolName(toolName));
    }

    [Fact]
    public void GenerateUnifiedDiff_ProducesValidDiff()
    {
        var oldText = "line1\nline2\nline3";
        var newText = "line1\nmodified\nline3";

        var diff = ToolItems.GenerateUnifiedDiff("test.txt", oldText, newText);

        Assert.Contains("--- a/test.txt", diff);
        Assert.Contains("+++ b/test.txt", diff);
        Assert.Contains("-line2", diff);
        Assert.Contains("+modified", diff);
    }
}

using System.Text;
using Markdig;
using SpecStory.Web.Models;

namespace SpecStory.Web.Services;

public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string GenerateMarkdown(SessionData session)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# {session.DisplayName}");
        sb.AppendLine();
        sb.AppendLine($"**Provider:** {session.Provider.Name}");
        sb.AppendLine($"**Session ID:** `{session.SessionId}`");

        if (!string.IsNullOrEmpty(session.CreatedAt))
            sb.AppendLine($"**Created:** {session.CreatedAt}");
        if (!string.IsNullOrEmpty(session.UpdatedAt))
            sb.AppendLine($"**Updated:** {session.UpdatedAt}");
        if (!string.IsNullOrEmpty(session.WorkspaceRoot))
            sb.AppendLine($"**Workspace:** `{session.WorkspaceRoot}`");

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Statistics
        sb.AppendLine("## Statistics");
        sb.AppendLine();
        sb.AppendLine($"- **Exchanges:** {session.Exchanges.Count}");
        sb.AppendLine($"- **Messages:** {session.TotalMessages}");
        sb.AppendLine($"- **Tool Uses:** {session.TotalToolUses}");
        sb.AppendLine($"- **Total Tokens:** {session.TotalTokens:N0}");

        if (session.Duration.HasValue)
            sb.AppendLine($"- **Duration:** {session.Duration.Value:hh\\:mm\\:ss}");

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Exchanges
        for (var i = 0; i < session.Exchanges.Count; i++)
        {
            var exchange = session.Exchanges[i];
            sb.AppendLine($"## Exchange {i + 1}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(exchange.StartTime))
                sb.AppendLine($"*Started: {exchange.StartTime}*");

            sb.AppendLine();

            foreach (var message in exchange.Messages)
            {
                var roleLabel = message.Role == "user" ? "**User**" : "**Agent**";
                var modelInfo = !string.IsNullOrEmpty(message.Model) ? $" ({message.Model})" : "";

                sb.AppendLine($"### {roleLabel}{modelInfo}");
                sb.AppendLine();

                // Thinking content
                foreach (var part in message.Content.Where(c => c.Type == "thinking"))
                {
                    sb.AppendLine("<details>");
                    sb.AppendLine("<summary>Thinking</summary>");
                    sb.AppendLine();
                    sb.AppendLine(part.Text);
                    sb.AppendLine();
                    sb.AppendLine("</details>");
                    sb.AppendLine();
                }

                // Text content
                foreach (var part in message.Content.Where(c => c.Type == "text"))
                {
                    sb.AppendLine(part.Text);
                    sb.AppendLine();
                }

                // Tool usage
                if (message.Tool != null)
                {
                    sb.AppendLine($"> **Tool:** `{message.Tool.Name}` ({message.Tool.Type})");

                    if (!string.IsNullOrEmpty(message.Tool.Summary))
                        sb.AppendLine($"> {message.Tool.Summary}");

                    if (!string.IsNullOrEmpty(message.Tool.FormattedMarkdown))
                    {
                        sb.AppendLine();
                        sb.AppendLine(message.Tool.FormattedMarkdown);
                    }

                    sb.AppendLine();
                }

                // Token usage
                if (message.Usage != null && message.Usage.TotalTokens > 0)
                {
                    sb.AppendLine($"*Tokens: {message.Usage.InputTokens:N0} in / " +
                                  $"{message.Usage.OutputTokens:N0} out*");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public string ConvertMarkdownToHtml(string markdown)
    {
        return Markdig.Markdown.ToHtml(markdown, _pipeline);
    }
}

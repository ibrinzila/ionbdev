using SpecStory.Web.Models;

namespace SpecStory.Web.Services;

public interface IMarkdownService
{
    string GenerateMarkdown(SessionData session);
    string ConvertMarkdownToHtml(string markdown);
}

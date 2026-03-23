namespace SpecStory.Web.Models;

public class ContentPart
{
    /// <summary>
    /// "text" or "thinking"
    /// </summary>
    public string Type { get; set; } = "text";
    public string Text { get; set; } = string.Empty;
}

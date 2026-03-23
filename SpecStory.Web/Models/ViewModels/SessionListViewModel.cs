namespace SpecStory.Web.Models.ViewModels;

public class SessionListViewModel
{
    public List<SessionMetadata> Sessions { get; set; } = new();
    public string? FilterProvider { get; set; }
    public string? SearchQuery { get; set; }
    public string SortBy { get; set; } = "created_desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public List<string> AvailableProviders { get; set; } = new();
}

using ClaudeAgentTeamsUI.Models;

namespace ClaudeAgentTeamsUI.Services;

public class ProjectService
{
    private readonly ILogger<ProjectService> _logger;
    private readonly string _claudeDir;
    private List<Project>? _projectCache;

    public ProjectService(ILogger<ProjectService> logger)
    {
        _logger = logger;
        _claudeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude");
    }

    public async Task<List<Project>> GetAllAsync()
    {
        if (_projectCache != null) return _projectCache;

        var projects = new List<Project>();
        var projectsDir = Path.Combine(_claudeDir, "projects");

        if (Directory.Exists(projectsDir))
        {
            foreach (var dir in Directory.GetDirectories(projectsDir))
            {
                var name = Path.GetFileName(dir);
                var sessionFiles = Directory.GetFiles(dir, "*.jsonl");
                projects.Add(new Project
                {
                    Id = name,
                    Name = name,
                    Path = dir,
                    SessionCount = sessionFiles.Length,
                    LastActivity = sessionFiles.Any()
                        ? sessionFiles.Max(f => File.GetLastWriteTimeUtc(f))
                        : (DateTime?)null
                });
            }
        }

        if (!projects.Any())
        {
            projects = GenerateDemoProjects();
        }

        _projectCache = projects;
        return await Task.FromResult(projects);
    }

    public async Task<List<RepositoryGroup>> GetRepositoryGroupsAsync()
    {
        var projects = await GetAllAsync();
        return projects
            .GroupBy(p => p.GitRemote ?? p.Path)
            .Select(g => new RepositoryGroup
            {
                RepositoryPath = g.Key,
                RemoteUrl = g.First().GitRemote,
                Projects = g.ToList()
            })
            .ToList();
    }

    public void InvalidateCache()
    {
        _projectCache = null;
    }

    private List<Project> GenerateDemoProjects()
    {
        return new List<Project>
        {
            new() { Id = "web-app", Name = "web-app", Path = "/home/user/projects/web-app",
                     GitRemote = "https://github.com/demo/web-app", GitBranch = "main", SessionCount = 5 },
            new() { Id = "api-service", Name = "api-service", Path = "/home/user/projects/api-service",
                     GitRemote = "https://github.com/demo/api-service", GitBranch = "develop", SessionCount = 3 },
            new() { Id = "mobile-client", Name = "mobile-client", Path = "/home/user/projects/mobile-client",
                     SessionCount = 4 },
            new() { Id = "data-pipeline", Name = "data-pipeline", Path = "/home/user/projects/data-pipeline",
                     GitRemote = "https://github.com/demo/data-pipeline", SessionCount = 2 },
            new() { Id = "infra-config", Name = "infra-config", Path = "/home/user/projects/infra-config",
                     SessionCount = 1 }
        };
    }
}

using Microsoft.Extensions.Configuration;
using Octokit;

namespace SDP.SoftwareRepositoryMining;

public static class Program
{
    #region Private Variables

    private static readonly string _applicationName = "SDP.SoftwareRepositoryMining";

    private static readonly string _githubTokenProperty = "GithubToken";

    private static readonly short _repositoryTakeFinalQuantity = 30;

    private static string _filteredRepositoriesFilePath = string.Empty;

    #endregion

    private static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets<Secrets>();

        var configuration = builder.Build();

        var githubToken = configuration[_githubTokenProperty];

        if (githubToken == null)
        {
            throw new ArgumentException(
                @"GithubToken must be set in Secrets before running the application.
                For windows users, please Right Click on the 'SDP.SoftwareRepositoryMining' Project,
                Select 'Manage User Secrets' option and add this JSON variable.
                In order to generate the Github Token...
                    1. Go to https://github.com/settings/personal-access-tokens/new
                        1.1 You might need to perform MFA before creating a fine-grained token.
                For MacOS users, follow readme file.

                ");
        }

        InitializeFilteredRepositoriesFullPath();

        var client = CreateGithubClient(_applicationName, githubToken);

        if (!HasContents(_filteredRepositoriesFilePath))
        {
            await GetFilteredRepositories(client);
        }


    }

    private static bool HasContents(string filePath)
    {
        if (File.Exists(filePath))
        {
            string fileContent = File.ReadAllText(filePath);
            return !string.IsNullOrEmpty(fileContent);
        }
        return false;
    }

    private static void InitializeFilteredRepositoriesFullPath()
    {
        string binPath = Directory.GetCurrentDirectory();

        string projectPath = Directory.GetParent(binPath)!.Parent!.Parent!.FullName;

        string filePath = Path.Combine(projectPath, "repos-results.txt");

        string fullPath = Path.GetFullPath(filePath);

        _filteredRepositoriesFilePath = fullPath;
    }

    private static void WriteResultsToTxt(IEnumerable<string> repositories)
    {
        File.WriteAllText(_filteredRepositoriesFilePath, string.Empty);

        File.WriteAllLines(_filteredRepositoriesFilePath, repositories);
    }

    private static GitHubClient CreateGithubClient(string appName, string githubToken)
    {
        var client = new GitHubClient(new ProductHeaderValue(appName));

        client.Credentials = new Credentials(githubToken);

        return client;
    }

    private static async Task GetFilteredRepositories(GitHubClient client)
    {
        var repositories = await client.Repository.GetAllForOrg("apache");

        var filteredRepositories = repositories
            .Where(repo => !repo.Archived && (repo.Language == "Java" || repo.Topics.Contains("java")))
            .ToList();

        var sortedByLinesOfCode = filteredRepositories.OrderByDescending(repo => repo.Size);

        var sortedByStars = filteredRepositories.OrderByDescending(repo => repo.StargazersCount);

        var sortedBySubcribers = filteredRepositories.OrderByDescending(repo => repo.SubscribersCount);

        var sortedByForks = filteredRepositories.OrderByDescending(repo => repo.ForksCount);

        var sortedByAge = filteredRepositories.OrderBy(repo => repo.CreatedAt);

        var sortedBySize = filteredRepositories.OrderByDescending(repo => repo.Size);

        var commonRepositories = sortedByLinesOfCode
            .Intersect(sortedByForks)
            .Intersect(sortedByStars)
            .Intersect(sortedBySubcribers)
            .Intersect(sortedByAge)
            .Intersect(sortedBySize)
            .Take(_repositoryTakeFinalQuantity)
            .ToList();

        var results = commonRepositories.Select(repo => repo.FullName).ToList();

        WriteResultsToTxt(results);
    }

    private static async Task<List<Repository>> GetReposSortedByContributors(GitHubClient client, List<Repository> repos, int take)
    {
        var repoContributors = new HashSet<(Repository repo, int contributors)>();

        foreach (var repo in repos)
        {
            var contributors = await client.Repository.Statistics.GetContributors(repo.Owner.Login, repo.Name);
            repoContributors.Add((repo, contributors.Count));
        }

        return repoContributors.OrderByDescending(rc => rc.contributors).Take(take).Select(rc => rc.repo).ToList();
    }
}
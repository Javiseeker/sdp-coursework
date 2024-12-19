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

        await CreateFilteredRepositoriesFile(client);

        //await AnalyzeCommitsFromGitHubRepository(client, "apache", "hadoop", "trunk", 1000);
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

    private static async Task CreateFilteredRepositoriesFile(GitHubClient client, bool overwriteFile = false)
    {
        if (!overwriteFile)
        {
            return;
        }

        // Get all repos from Apache
        var repositories = await client.Repository.GetAllForOrg("apache");

        // Filter all repos by those that are NOT archived
        // and whose language or topics contain Java Programming Language
        var filteredRepositories = repositories
            .Where(repo => !repo.Archived && (repo.Language == "Java" || repo.Topics.Contains("java")))
            .ToList();

        // sort them by size.
        var sortedByLinesOfCode = filteredRepositories.OrderByDescending(repo => repo.Size);

        // sort them by Stars
        var sortedByStars = filteredRepositories.OrderByDescending(repo => repo.StargazersCount);

        // sort them by subscribers
        var sortedBySubcribers = filteredRepositories.OrderByDescending(repo => repo.SubscribersCount);

        // sort them by forks
        var sortedByForks = filteredRepositories.OrderByDescending(repo => repo.ForksCount);

        // sort them by created date
        var sortedByAge = filteredRepositories.OrderBy(repo => repo.CreatedAt);

        // sort them by 
        var sortedBySize = filteredRepositories.OrderByDescending(repo => repo.Size);

        // find intersections and take the desired quantity
        var commonRepositories = sortedByLinesOfCode
            .Intersect(sortedByForks)
            .Intersect(sortedByStars)
            .Intersect(sortedBySubcribers)
            .Intersect(sortedByAge)
            .Intersect(sortedBySize)
            .Take(_repositoryTakeFinalQuantity)
            .ToList();

        // select the full name of the repository.
        var results = commonRepositories.Select(repo => repo.FullName).ToList();

        WriteResultsToTxt(results);

        PrintResults(results);
    }

    private static void PrintResults(List<string> results)
    {
        Console.WriteLine("\n=== Results ===\n");

        if (results == null || !results.Any())
        {
            Console.WriteLine("No results found.");
        }
        else
        {
            for (int i = 0; i < results.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {results[i]}");
            }
            Console.WriteLine($"\nTotal items: {results.Count}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    public static async Task GetCommitsAndContentsOfPullRequest(GitHubClient client, string repositoryName, int pullRequestNumber)
    {
        var repoOwner = "apache";
        var repoName = repositoryName;

        var commits = await client.PullRequest.Commits(repoOwner, repoName, pullRequestNumber);

        foreach (var commit in commits)
        {
            Console.WriteLine($"Commit: {commit.Commit.Message}");

            var commitDetails = await client.Repository.Commit.Get(repoOwner, repoName, commit.Sha);
            foreach (var file in commitDetails.Files)
            {
                Console.WriteLine($"File: {file.Filename}");
                Console.WriteLine($"Changes: {file.Patch}");
            }
        }
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

    private static async Task AnalyzeCommitsFromGitHubRepository(GitHubClient client, string owner, string repository, string branch, int commitsToAnalyze)
    {
        try
        {
            var apiOptions = new ApiOptions
            {
                PageSize = 1,
                PageCount = commitsToAnalyze
            };

            Console.WriteLine($"\nAnalyzing commits for {owner}/{repository} on branch {branch}");
            Console.WriteLine("----------------------------------------");

            // Get reference to get the branch SHA
            var reference = await client.Git.Reference.Get(owner, repository, $"refs/heads/{branch}");

            // Get commits using the correct pagination
            var allCommits = new List<GitHubCommit>();
            var commits = await client.Repository.Commit.GetAll(owner, repository, new CommitRequest
            {
                Sha = reference.Object.Sha
            }, apiOptions);

            allCommits.AddRange(commits);

            int totalCommits = allCommits.Count;
            int directCommits = 0;
            int prCommits = 0;
            int processed = 0;

            foreach (var commit in allCommits)
            {
                try
                {
                    // Get pull requests associated with this commit
                    var prs = await client.Repository.Commit.PullRequests(owner, repo, commit.Sha);

                    if (!prs.Any())
                    {
                        directCommits++;
                    }
                    else
                    {
                        prCommits++;
                    }

                    processed++;
                    Console.Write($"\rProcessing commits: {processed}/{totalCommits}");
                }
                catch (RateLimitExceededException rateEx)
                {
                    Console.WriteLine($"\nRate limit exceeded. Reset at: {rateEx.Reset}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError processing commit {commit.Sha}: {ex.Message}");
                }

                // Add a small delay to avoid hitting rate limits too quickly
                await Task.Delay(100);
            }

            // Calculate percentages
            double percentageDirect = totalCommits > 0 ? (double)directCommits / totalCommits * 100 : 0;

            // Print results
            Console.WriteLine("\n\nResults:");
            Console.WriteLine($"Total commits analyzed: {totalCommits}");
            Console.WriteLine($"Direct commits: {directCommits}");
            Console.WriteLine($"PR-merged commits: {prCommits}");
            Console.WriteLine($"Percentage of direct commits: {percentageDirect:F2}%");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        catch (NotFoundException)
        {
            Console.WriteLine("Repository or branch not found. Please check the owner, repo, and branch names.");
        }
        catch (RateLimitExceededException rateEx)
        {
            Console.WriteLine($"Rate limit exceeded. Reset at: {rateEx.Reset}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
using Lucene.Net.Support;
using NuGetGallery.Configuration;
using Octokit;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class SourceRepoService : ISourceRepoService
    {
        protected IAppConfiguration Config { get; set; }

        public SourceRepoService(IAppConfiguration config)
        {
            Config = config;
        }

        public Task<SourceRepositoryViewModel> Load(Package package)
        {
            if (null == package) throw new ArgumentNullException(nameof(package));
            return Load(package.ProjectUrl);
        }

        Task<SourceRepositoryViewModel> Load(string url)
        {
            string owner = null;
            string repo = null;

            if (IsGithubUrl(url, out owner, out repo)) return LookupGithub(owner, repo);

            // add other source code repositories here...

            return null;
        }

        public bool IsGithubUrl(string url, out string owner, out string repo)
        {
            owner = null;
            repo = null;
            if (string.IsNullOrWhiteSpace(url)) return false;

            var uri = new Uri(url);

            if (string.Compare(uri.Host, "github.com", true) != 0) return false;
            if (uri.Segments.Length < 3) return false;

            // if the path is "/user/repo" the segments will be ["/", "user/", "repo"]
            owner = uri.Segments.Skip(1).First().Replace(@"/", "");
            repo = uri.Segments.Skip(2).First().Replace(@"/", "");
            return true;
        }

        async Task<SourceRepositoryViewModel> LookupGithub(string owner, string repo)
        {
            // no credentials disables the lookup
            if (string.IsNullOrWhiteSpace(Config.GithubUsername)) return null;
            if (string.IsNullOrWhiteSpace(Config.GithubPassword)) return null;

            var github = new GitHubClient(new ProductHeaderValue("NugetGallery"));
            github.Credentials = new Credentials(Config.GithubUsername, Config.GithubPassword);

            var repositoryTask = github.Repository.Get(owner, repo);
            var readmeTask = github.Repository.Content.GetReadmeHtml(owner, repo);

            // run API queries in parallel
            await Task.WhenAll(repositoryTask, readmeTask);

            if (repositoryTask.IsFaulted) return null;

            var repository = repositoryTask.Result;

            var result = new SourceRepositoryViewModel
            {
                AvatarUrl = repository.Owner.AvatarUrl,
                Owner = repository.Owner.Login,
                Name = repository.Name,
                ProgrammingLanguage = repository.Language,
                StarCount = repository.StargazersCount,
                ForkCount = repository.ForksCount,
                OpenIssueCount = repository.OpenIssuesCount,
                LastUpdated = repository.UpdatedAt,
                URL = string.Format("https://github.com/{0}/{1}", owner, repo),
                ReadmeHTML = readmeTask.Result 
            };

            return result;
        }

    }
}
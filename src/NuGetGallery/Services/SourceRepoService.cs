using Lucene.Net.Support;
using NuGetGallery.Configuration;
using Octokit;
using SharpBucket.V1;
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

            if (IsGitHubUrl(url, out owner, out repo)) return LookupGitHub(owner, repo);
            if (IsBitbucketUrl(url, out owner, out repo)) return LookupBitbucket(owner, repo);

            // add other source code repositories here...

            return null;
        }

        public bool IsGitHubUrl(string url, out string owner, out string repo)
        {
            return CheckHostName(url, "github.com", out owner, out repo);
        }

        public bool IsBitbucketUrl(string url, out string owner, out string repo)
        {
            return CheckHostName(url, "bitbucket.org", out owner, out repo);
        }

        public bool CheckHostName(string url, string host, out string owner, out string repo)
        {
            owner = null;
            repo = null;
            if (string.IsNullOrWhiteSpace(url)) return false;

            var uri = new Uri(url);

            if (string.Compare(uri.Host, host, true) != 0) return false;
            if (uri.Segments.Length < 3) return false;

            // if the path is "/user/repo" the segments will be ["/", "user/", "repo"]
            owner = uri.Segments.Skip(1).First().Replace(@"/", "");
            repo = uri.Segments.Skip(2).First().Replace(@"/", "");
            return true;
        }

        async Task<SourceRepositoryViewModel> LookupGitHub(string owner, string repo)
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
                Origin = "GitHub",
                AvatarUrl = repository.Owner.AvatarUrl,
                Owner = repository.Owner.Login,
                Name = repository.Name,
                ProgrammingLanguage = repository.Language,
                StarCount = repository.StargazersCount,
                ForkCount = repository.ForksCount,
                FollowersCount = -1,
                OpenIssueCount = repository.OpenIssuesCount,
                LastUpdated = repository.UpdatedAt,
                URL = string.Format("https://github.com/{0}/{1}", owner, repo),
                ReadmeHTML = readmeTask.Result 
            };

            return result;
        }

        Task<SourceRepositoryViewModel> LookupBitbucket(string owner, string repo)
        {
            // no credentials disables the lookup
            if (string.IsNullOrWhiteSpace(Config.BitbucketUsername)) return null;
            if (string.IsNullOrWhiteSpace(Config.BitbucketPassword)) return null;

            var sharpBucket = new SharpBucketV1();
            sharpBucket.BasicAuthentication(Config.BitbucketUsername, Config.BitbucketPassword);

            var repository = sharpBucket.RepositoriesEndPoint(owner, repo);
            var repositoryDetails = repository.GetDetails();
            var branch = repository.GetMainBranch();
            var files = repository.GetRevisionSrc(branch.name, "");
            var readmeFile = GetReadme(files.files.Select(x => x.path).ToArray());
            var readmeText = repository.GetRevisionRaw(branch.name, readmeFile);
            var readmeHtml = CommonMark.CommonMarkConverter.Convert(readmeText);

            return Task.FromResult(new SourceRepositoryViewModel
            {
                Origin = "Bitbucket",
                AvatarUrl = repositoryDetails.logo,
                Owner = repositoryDetails.owner,
                Name = repositoryDetails.name,
                ProgrammingLanguage = repositoryDetails.language,
                FollowersCount = repositoryDetails.followers_count,
                StarCount = -1,
                ForkCount = repositoryDetails.fork_count,
                OpenIssueCount = -1, // not supported
                LastUpdated = DateTimeOffset.Parse(repositoryDetails.utc_last_updated),
                URL = string.Format("https://bitbucket.org/{0}/{1}", owner, repo),
                ReadmeHTML = readmeHtml
            });
        }



        private static string GetReadme(string[] values)
        {
            string result = null;
            result = values.FirstOrDefault(x => x.ToLower() == "readme.md");
            if (result != null) return result;

            result = values.FirstOrDefault(x => x.ToLower().StartsWith("readme"));
            if (result != null) return result;

            return values.FirstOrDefault(x => x.ToLower().EndsWith(".md"));
        }

    }
}
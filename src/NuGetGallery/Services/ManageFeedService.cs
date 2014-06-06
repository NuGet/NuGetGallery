using NuGet;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class ManageFeedService : IManageFeedService
    {
        protected IEntityRepository<Feed> FeedRepository { get; set; }

        public ManageFeedService(IEntityRepository<Feed> feedRepository)
        {
            FeedRepository = feedRepository;
        }

        public Feed GetFeedByName(string feedName)
        {
            return FeedRepository.GetAll()
                .Include(f => f.Packages)
                .Include(f => f.Packages.Select(fp => fp.Package))
                .Include(f => f.Rules)
                .Include(f => f.Rules.Select(r => r.PackageRegistration))
                .SingleOrDefault(f => f.Name == feedName);
        }

        public IEnumerable<Feed> GetFeedsForManager(int managerKey)
        {
            return FeedRepository.GetAll()
                .Where(f => f.Managers.Any(u => u.Key == managerKey));
        }

        public void CreateFeedRule(Feed feed, PackageRegistration packageRegistration, string packageVersionSpec, string notes)
        {
            FeedRule newRule = new FeedRule
            {
                Feed = feed,
                FeedKey = feed.Key,
                PackageRegistration = packageRegistration,
                PackageRegistrationKey = packageRegistration.Key,
                PackageVersionSpec = packageVersionSpec,
                Notes = notes
            };

            feed.Rules.Add(newRule);

            HashSet<int> packageKeyInFeed = new HashSet<int>(feed.Packages.Select(fp => fp.PackageKey));

            DateTime timeStamp = DateTime.UtcNow;

            foreach (Package package in packageRegistration.Packages)
            {
                SemanticVersion semanticVersion = SemanticVersion.Parse(package.NormalizedVersion);
                IVersionSpec versionSpec = VersionUtility.ParseVersionSpec(packageVersionSpec);

                if (versionSpec.Satisfies(semanticVersion) && !packageKeyInFeed.Contains(package.Key))
                {
                    FeedPackage feedPackage = new FeedPackage
                    {
                        Feed = feed,
                        FeedKey = feed.Key,
                        Package = package,
                        PackageKey = package.Key,
                        Added = timeStamp,
                        IsLatest = false,
                        IsLatestStable = false
                    };

                    feed.Packages.Add(feedPackage);
                }
            }

            UpdateIsLatest(feed, packageRegistration);

            FeedRepository.CommitChanges();
        }

        static void UpdateIsLatest(Feed feed, PackageRegistration packageRegistration)
        {
            var feedPackages = feed.Packages.Where((fp) => fp.Package.PackageRegistrationKey == packageRegistration.Key);

            FeedPackage isLatestCandidate = feedPackages.First();
            FeedPackage isLatestStableCandidate = feedPackages.First();

            foreach (var feedPackage in feedPackages)
            {
                SemanticVersion feedPackageSemanticVersion = SemanticVersion.Parse(feedPackage.Package.NormalizedVersion);
                SemanticVersion currentCandidateSemanticVersion = SemanticVersion.Parse(isLatestCandidate.Package.NormalizedVersion);

                if (feedPackageSemanticVersion > currentCandidateSemanticVersion)
                {
                    isLatestCandidate = feedPackage;
                }
                else
                {
                    feedPackage.IsLatest = false;
                }

                if (feedPackageSemanticVersion > currentCandidateSemanticVersion && !feedPackage.Package.IsPrerelease)
                {
                    isLatestStableCandidate = feedPackage;
                }
                else
                {
                    feedPackage.IsLatestStable = false;
                }
            }

            isLatestCandidate.IsLatest = true;
            isLatestStableCandidate.IsLatestStable = true;
        }
    }
}
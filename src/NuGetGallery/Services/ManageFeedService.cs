using NuGet;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Objects;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class ManageFeedService : IManageFeedService
    {
        protected IEntityRepository<Feed> FeedRepository { get; set; }
        protected IEntitiesContext EntitiesContext { get; set; }

        public ManageFeedService(IEntityRepository<Feed> feedRepository, IEntitiesContext entitiesContext)
        {
            FeedRepository = feedRepository;
            EntitiesContext = entitiesContext;
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

            if (feed.Inclusive)
            {
                IncludePackagesInFeed(feed, packageRegistration, packageVersionSpec);
            }
            else
            {
                ExcludePackagesFromFeed(feed, packageRegistration, packageVersionSpec);
            }

            UpdateIsLatest(feed, packageRegistration);

            FeedRepository.CommitChanges();
        }

        public void DeleteFeedRule(Feed feed, string id, string versionSpec)
        {
            FeedRule feedRule = feed.Rules
                .SingleOrDefault(fr => fr.PackageRegistration.Id == id && fr.PackageVersionSpec == versionSpec);

            if (feedRule != null)
            {
                EntitiesContext.DeleteOnCommit(feedRule);
                feed.Rules.Remove(feedRule);

                PackageRegistration packageRegistration = EntitiesContext.PackageRegistrations
                    .Where(pr => pr.Id == id)
                    .FirstOrDefault();

                if (packageRegistration != null)
                {
                    RecalculateFeedPackages(feed, packageRegistration);
                    UpdateIsLatest(feed, packageRegistration);
                }

                FeedRepository.CommitChanges();
            }
        }

        void RecalculateFeedPackages(Feed feed, PackageRegistration packageRegistration)
        {
            if (feed.Inclusive)
            {
                RecalculateFeedPackageInclude(feed, packageRegistration);
            }
            else
            {
                RecalculateFeedPackageExclude(feed, packageRegistration);
            }
        }

        void RecalculateFeedPackageInclude(Feed feed, PackageRegistration packageRegistration)
        {
            HashSet<int> packageKeyInFeed = new HashSet<int>(feed.Packages.Select(fp => fp.PackageKey));

            HashSet<int> recalculatedPackageSet = CalculateMatchingPackageSet(feed, packageRegistration);

            DateTime timeStamp = DateTime.UtcNow;

            foreach (int packageKey in recalculatedPackageSet)
            {
                if (!packageKeyInFeed.Contains(packageKey))
                {
                    Package package = packageRegistration.Packages.SingleOrDefault(p => p.Key == packageKey);

                    if (package != null)
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
            }

            foreach (int packageKey in packageKeyInFeed)
            {
                if (!recalculatedPackageSet.Contains(packageKey))
                {
                    FeedPackage feedPackage = feed.Packages.SingleOrDefault(fp => fp.PackageKey == packageKey);

                    feed.Packages.Remove(feedPackage);
                    EntitiesContext.DeleteOnCommit(feedPackage);
                }
            }
        }

        static HashSet<int> CalculateMatchingPackageSet(Feed feed, PackageRegistration packageRegistration)
        {
            HashSet<int> result = new HashSet<int>();

            foreach (FeedRule rule in feed.Rules)
            {
                IVersionSpec versionSpec = VersionUtility.ParseVersionSpec(rule.PackageVersionSpec);

                foreach (Package package in packageRegistration.Packages)
                {
                    SemanticVersion semanticVersion = SemanticVersion.Parse(package.NormalizedVersion);

                    if (versionSpec.Satisfies(semanticVersion))
                    {
                        result.Add(package.Key);
                    }
                }
            }

            return result;
        }

        void RecalculateFeedPackageExclude(Feed feed, PackageRegistration packageRegistration)
        {
            HashSet<int> packageKeyInFeed = new HashSet<int>(feed.Packages.Select(fp => fp.PackageKey));

            HashSet<int> packageKeyInRepository = new HashSet<int>(packageRegistration.Packages.Select(p => p.Key));

            HashSet<int> recalculatedPackageSet = CalculateMatchingPackageSet(feed, packageRegistration);

            DateTime timeStamp = DateTime.UtcNow;

            foreach (int packageKey in packageKeyInRepository)
            {
                if (!recalculatedPackageSet.Contains(packageKey) && !packageKeyInFeed.Contains(packageKey))
                {
                    Package package = packageRegistration.Packages.SingleOrDefault(p => p.Key == packageKey);

                    if (package != null)
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
            }

            foreach (int packageKey in packageKeyInFeed)
            {
                if (recalculatedPackageSet.Contains(packageKey))
                {
                    FeedPackage feedPackage = feed.Packages.SingleOrDefault(fp => fp.PackageKey == packageKey);

                    feed.Packages.Remove(feedPackage);
                    EntitiesContext.DeleteOnCommit(feedPackage);
                }
            }
        }

        static void IncludePackagesInFeed(Feed feed, PackageRegistration packageRegistration, string packageVersionSpec)
        {
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
        }

        void ExcludePackagesFromFeed(Feed feed, PackageRegistration packageRegistration, string packageVersionSpec)
        {
            HashSet<int> packageKeyInFeed = new HashSet<int>(feed.Packages.Select(fp => fp.PackageKey));

            DateTime timeStamp = DateTime.UtcNow;

            HashSet<int> packagesExcludedByRule = new HashSet<int>();

            foreach (Package package in packageRegistration.Packages)
            {
                SemanticVersion semanticVersion = SemanticVersion.Parse(package.NormalizedVersion);
                IVersionSpec versionSpec = VersionUtility.ParseVersionSpec(packageVersionSpec);

                if (versionSpec.Satisfies(semanticVersion) && packageKeyInFeed.Contains(package.Key))
                {
                    packagesExcludedByRule.Add(package.Key);
                }
            }

            List<FeedPackage> deletedFeedPackages = new List<FeedPackage>();
            foreach (FeedPackage feedPackage in feed.Packages)
            {
                if (packagesExcludedByRule.Contains(feedPackage.PackageKey) && packageKeyInFeed.Contains(feedPackage.PackageKey))
                {
                    deletedFeedPackages.Add(feedPackage);
                }
            }

            foreach (FeedPackage feedPackage in deletedFeedPackages)
            {
                EntitiesContext.DeleteOnCommit(feedPackage);
                feed.Packages.Remove(feedPackage);
            }
        }

        static void UpdateIsLatest(Feed feed, PackageRegistration packageRegistration)
        {
            var feedPackages = feed.Packages.Where((fp) => fp.Package.PackageRegistrationKey == packageRegistration.Key);

            if (feedPackages.Count() > 0)
            {
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
}
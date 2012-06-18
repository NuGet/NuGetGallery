using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGet;

namespace NuGetGallery
{
    public abstract class TagBasedPackageCurator : AutomaticPackageCurator
    {
        /// <summary>
        /// Gets a list of tags required for a package to be selected by this curator. A package MUST have ONE of the specified tags to be curated.
        /// </summary>
        protected abstract IEnumerable<string> RequiredTags { get; }

        /// <summary>
        /// Gets the name of the curated feed to add the package to.
        /// </summary>
        protected abstract string CuratedFeedName { get; }

        public override void Curate(Package galleryPackage, IPackage nugetPackage)
        {
            // Check if this package should be curated
            if (galleryPackage.Tags != null && RequiredTags.Any(s => galleryPackage.Tags.ToLowerInvariant().Contains(s)))
            {
                // It should! Add it to the curated feed
                AddPackageToFeed(galleryPackage);
            }
        }

        protected virtual void AddPackageToFeed(Package galleryPackage)
        {
            CuratedFeed feed = GetTargetFeed();
            GetService<ICreateCuratedPackageCommand>().Execute(
                feed.Key, galleryPackage.PackageRegistration.Key, automaticallyCurated: true);
        }

        protected virtual CuratedFeed GetTargetFeed()
        {
            return GetService<ICuratedFeedByNameQuery>().Execute(CuratedFeedName, includePackages: false);
        }
    }
}
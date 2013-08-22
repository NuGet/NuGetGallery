using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public abstract class TagBasedPackageCurator : AutomaticPackageCurator
    {
        /// <summary>
        ///     Gets a list of tags required for a package to be selected by this curator. A package MUST have ONE of the specified tags to be curated.
        /// </summary>
        protected abstract IEnumerable<string> RequiredTags { get; }

        /// <summary>
        ///     Gets the name of the curated feed to add the package to.
        /// </summary>
        protected abstract string CuratedFeedName { get; }

        public override void Curate(Package galleryPackage, INupkg nugetPackage, bool commitChanges)
        {
            // Make sure the target feed exists
            CuratedFeed feed = GetService<ICuratedFeedService>().GetFeedByName(CuratedFeedName, includePackages: true);
            if (feed != null && galleryPackage.Tags != null)
            {
                // Break the tags up so we can be sure we don't catch any partial matches (i.e. "foobar" when we're looking for "foo")
                string[] tags = galleryPackage.Tags.Split();

                // Check if this package should be curated
                if (tags.Any(tag => RequiredTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                {
                    // It should!
                    // But now we need to ensure that the package's dependencies are also curated
                    if (DependenciesAreCurated(galleryPackage, feed))
                    {
                        GetService<ICuratedFeedService>().CreatedCuratedPackage(
                            feed, 
                            galleryPackage.PackageRegistration, 
                            automaticallyCurated: true,
                            commitChanges: commitChanges);
                    }
                }
            }
        }
    }
}
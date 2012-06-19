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
            // Make sure the target feed exists
            CuratedFeed feed = GetService<ICuratedFeedByNameQuery>().Execute(CuratedFeedName, includePackages: false);
            if (feed != null && galleryPackage.Tags != null)
            {
                // Break the tags up so we can be sure we don't catch any partial matches (i.e. "foobar" when we're looking for "foo")
                string[] tags = galleryPackage.Tags.Split();

                // Check if this package should be curated
                if (tags.Any(tag => RequiredTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                {
                    // It should! Add it to the curated feed
                    GetService<ICreateCuratedPackageCommand>().Execute(
                        feed.Key, galleryPackage.PackageRegistration.Key, automaticallyCurated: true);
                }

            }
        }
    }
}
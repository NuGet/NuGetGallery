using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// Item to be displayed in search results for packages to import.
    /// </summary>
    public class ImportSearchItemViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportSearchItemViewModel"/> class.
        /// </summary>
        /// <param name="package">The package to display.</param>
        public ImportSearchItemViewModel(NuGetService.V2FeedPackage package)
        {
            Version = package.Version;
            Description = package.Description;
            ReleaseNotes = package.ReleaseNotes;
            IconUrl = package.IconUrl;
            ProjectUrl = package.ProjectUrl;
            LicenseUrl = package.LicenseUrl;
            LatestVersion = package.IsLatestVersion;
            LatestStableVersion = !package.IsPrerelease;
            LastUpdated = package.Published;
            DownloadCount = package.DownloadCount;
            Prerelease = package.IsPrerelease;
            Title = package.Title;
            Id = package.Id;

            Tags = package.Tags != null ? package.Tags.Trim().Split(' ') : null;
            Authors = package.Authors.Split(',').Select(a => a.Trim());

            MinClientVersion = package.MinClientVersion;
        }

        public string Description { get; set; }
        public string ReleaseNotes { get; set; }
        public string IconUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string LicenseUrl { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool LatestVersion { get; set; }
        public bool LatestStableVersion { get; set; }
        public bool Prerelease { get; set; }
        public int DownloadCount { get; set; }

        public string Id { get; set; }

        public string Version { get; set; }

        public string Title { get; set; }

        public bool IsCurrent(ImportSearchItemViewModel current)
        {
            return current.Version == Version && current.Id == Id;
        }

        public IEnumerable<string> Authors { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public string MinClientVersion { get; set; }

        public bool UseVersion
        {
            get
            {
                // only show the version when we'll end up listing the package more than once. This would happen when the latest version is not the same as the latest stable version.
                return !(LatestVersion && LatestStableVersion);
            }
        }

        public bool IsOwner(IPrincipal user)
        {
            if (user == null || user.Identity == null)
            {
                return false;
            }
            return user.IsAdministrator();
        }
    }
}
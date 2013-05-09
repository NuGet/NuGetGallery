using System.Collections.Generic;
using Microsoft.Internal.Web.Utils;
using NuGetGallery.Statistics;

namespace NuGetGallery
{
    public class StatisticsSummaryViewModel
    {
        public PackageDownloadsReport PackageDownloads { get; private set; }
        public PackageDownloadsReport PackageVersionDownloads { get; private set; }

        public StatisticsSummaryViewModel(PackageDownloadsReport packageDownloads, PackageDownloadsReport packageVersionDownloads)
        {
            PackageDownloads = packageDownloads;
            PackageVersionDownloads = packageVersionDownloads;
        }

        // Equals makes testing easier!! It's also just a Good Thing
        public override bool Equals(object obj)
        {
            StatisticsSummaryViewModel other = obj as StatisticsSummaryViewModel;
            return other != null &&
                Equals(PackageDownloads, other.PackageDownloads) &&
                Equals(PackageVersionDownloads, other.PackageVersionDownloads);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(PackageDownloads)
                .Add(PackageVersionDownloads)
                .CombinedHash;
        }
    }
}

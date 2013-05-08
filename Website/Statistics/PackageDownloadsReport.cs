using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Web;
using Microsoft.Internal.Web.Utils;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadsReport
    {
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "The type is immutable")]
        public static readonly PackageDownloadsReport Empty = new PackageDownloadsReport();

        public IEnumerable<PackageDownloadsReportEntry> Entries { get; private set; }

        private PackageDownloadsReport() : this(Enumerable.Empty<PackageDownloadsReportEntry>()) { }
        public PackageDownloadsReport(IEnumerable<PackageDownloadsReportEntry> entries)
        {
            Entries = entries;
        }

        public override bool Equals(object obj)
        {
            PackageDownloadsReport other = obj as PackageDownloadsReport;
            return other != null && Entries.SequenceEqual(other.Entries);
        }

        public override int GetHashCode()
        {
            return Entries.GetHashCode();
        }

        // Debugging aid
        public override string ToString()
        {
            return "[" + String.Join(",", Entries) + "]";
        }
    }

    public class PackageDownloadsReportEntry
    {
        public string PackageId { get; set; }
        public int Downloads { get; set; }
        public string PackageVersion { get; set; }
        public string PackageTitle { get; set; }
        public string PackageDescription { get; set; }
        public string PackageIconUrl { get; set; }

        public override bool Equals(object obj)
        {
            PackageDownloadsReportEntry other = obj as PackageDownloadsReportEntry;
            return other != null && 
                   String.Equals(PackageId, other.PackageId, StringComparison.Ordinal) &&
                   String.Equals(PackageVersion, other.PackageVersion, StringComparison.Ordinal) &&
                   String.Equals(PackageTitle, other.PackageTitle, StringComparison.Ordinal) &&
                   String.Equals(PackageDescription, other.PackageDescription, StringComparison.Ordinal) &&
                   String.Equals(PackageIconUrl, other.PackageIconUrl, StringComparison.Ordinal) &&
                   Downloads == other.Downloads;
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(PackageId)
                .Add(Downloads)
                .Add(PackageVersion)
                .Add(PackageTitle)
                .Add(PackageDescription)
                .Add(PackageIconUrl)
                .CombinedHash;
        }

        // Debugging aid
        public override string ToString()
        {
            return 
                "{ PackageId: '" + 
                PackageId + 
                "', Downloads: " + 
                Downloads.ToString(CultureInfo.InvariantCulture) +
                ", PackageVersion: '" +
                PackageVersion +
                "', PackageTitle: '" + 
                PackageTitle +
                "', PackageDescription: '" + 
                PackageDescription +
                "', PackageIconUrl: '" + 
                "' }";
        }
    }
}
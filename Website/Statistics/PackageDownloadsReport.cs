using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Internal.Web.Utils;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadsReport
    {
        public IEnumerable<PackageDownloadsReportEntry> Entries { get; private set; }

        public PackageDownloadsReport() : this(Enumerable.Empty<PackageDownloadsReportEntry>()) { }
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
    }

    public class PackageDownloadsReportEntry
    {
        public string PackageId { get; set; }
        public int Downloads { get; set; }

        public override bool Equals(object obj)
        {
            PackageDownloadsReportEntry other = obj as PackageDownloadsReportEntry;
            return other != null && 
                   String.Equals(PackageId, other.PackageId, StringComparison.Ordinal) && 
                   Downloads == other.Downloads;
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(PackageId)
                .Add(Downloads)
                .CombinedHash;
        }
    }
}
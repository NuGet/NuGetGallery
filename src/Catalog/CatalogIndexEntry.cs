using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class CatalogIndexEntry : IComparable<CatalogIndexEntry>
    {
        private readonly Uri _uri;
        private readonly string _type;
        private readonly string _commitId;
        private readonly DateTime _commitTimeStamp;
        private readonly string _id;
        private readonly NuGetVersion _version;

        public CatalogIndexEntry(Uri uri, string type, string commitId, DateTime commitTs, string id, NuGetVersion version)
        {
            _uri = uri;
            _type = type;
            _commitId = commitId;
            _commitTimeStamp = commitTs;
            _id = id;
            _version = version;
        }

        public Uri Uri
        {
            get
            {
                return _uri;
            }
        }

        public IEnumerable<string> Types
        {
            get
            {
                return new string[] { _type };
            }
        }

        public string Id
        {
            get
            {
                return _id;
            }
        }

        public NuGetVersion Version
        {
            get
            {
                return _version;
            }
        }

        public string CommitId
        {
            get
            {
                return _commitId;
            }
        }


        public DateTime CommitTimeStamp
        {
            get
            {
                return _commitTimeStamp;
            }
        }

        public int CompareTo(CatalogIndexEntry other)
        {
            return CommitTSComparer.Compare(this, other);
        }

        // common comparers for sorting and creating sets from these entries
        public static CatalogIndexEntryIdComparer IdComparer
        {
            get
            {
                return new CatalogIndexEntryIdComparer();
            }
        }

        public static CatalogIndexEntryDateComparer CommitTSComparer
        {
            get
            {
                return new CatalogIndexEntryDateComparer();
            }
        }
    }

    public class CatalogIndexEntryDateComparer : IComparer<CatalogIndexEntry>
    {
        public int Compare(CatalogIndexEntry x, CatalogIndexEntry y)
        {
            return x.CommitTimeStamp.CompareTo(y.CommitTimeStamp);
        }
    }


    public class CatalogIndexEntryIdComparer : IEqualityComparer<CatalogIndexEntry>
    {
        public bool Equals(CatalogIndexEntry x, CatalogIndexEntry y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id);
        }

        public int GetHashCode(CatalogIndexEntry obj)
        {
            return obj.Id.ToLowerInvariant().GetHashCode();
        }
    }

    public class CatalogIndexEntryPackageComparer : IEqualityComparer<CatalogIndexEntry>
    {
        const string PackageIdFormat = "{0}.{1}";
        public bool Equals(CatalogIndexEntry x, CatalogIndexEntry y)
        {
            return x.Id.Equals(y.Id, StringComparison.OrdinalIgnoreCase) && x.Version.Equals(y.Version);
        }

        public int GetHashCode(CatalogIndexEntry obj)
        {
            return String.Format(PackageIdFormat, obj.Id.ToLowerInvariant(), obj.Version.ToNormalizedString().ToLowerInvariant()).GetHashCode();
        }
    }
}

using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
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

        public string Id
        {
            get
            {
                return _id;
            }
        }

        public DateTime CommitTimeStamp
        {
            get
            {
                return _commitTimeStamp;
            }
        }

        /// <summary>
        /// Compare on the TS
        /// </summary>
        public static int Compare(CatalogIndexEntry x, CatalogIndexEntry y)
        {
            return x.CommitTimeStamp.CompareTo(y.CommitTimeStamp);
        }

        public int CompareTo(CatalogIndexEntry other)
        {
            return Compare(this, other);
        }

        public static CatalogIndexEntryIdComparer IdComparer
        {
            get
            {
                return new CatalogIndexEntryIdComparer();
            }
        }
    }

    public class CatalogIndexEntryIdComparer : IEqualityComparer<CatalogIndexEntry>
    {
        public CatalogIndexEntryIdComparer()
        {

        }

        public bool Equals(CatalogIndexEntry x, CatalogIndexEntry y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id);
        }

        public int GetHashCode(CatalogIndexEntry obj)
        {
            return obj.Id.ToLowerInvariant().GetHashCode();
        }
    }
}

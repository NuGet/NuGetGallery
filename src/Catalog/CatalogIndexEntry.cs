// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

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

        public CatalogIndexEntry(JToken item)
            : this(
                new Uri(item["@id"].ToString()),
                item["@type"].ToString(),
                item["commitId"].ToString(),
                DateTime.Parse(item["commitTimeStamp"].ToString()),
                item["nuget:id"].ToString(),
                NuGetVersion.Parse(item["nuget:version"].ToString()))
        {
        }

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

        public bool IsDelete()
        {
            return _type == "nuget:PackageDelete";
        }

        // common comparers for sorting and creating sets from these entries
        public static CatalogIndexEntryIdComparer IdComparer
        {
            get
            {
                return new CatalogIndexEntryIdComparer();
            }
        }

        public static CatalogIndexEntryIdVersionComparer IdVersionComparer
        {
            get
            {
                return new CatalogIndexEntryIdVersionComparer();
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

    public class CatalogIndexEntryIdVersionComparer : IEqualityComparer<CatalogIndexEntry>
    {
        const string PackageIdFormat = "{0}.{1}";
        public bool Equals(CatalogIndexEntry x, CatalogIndexEntry y)
        {
            return x.Id.Equals(y.Id, StringComparison.OrdinalIgnoreCase) && x.Version.Equals(y.Version);
        }

        public int GetHashCode(CatalogIndexEntry obj)
        {
            return String.Format(CultureInfo.InvariantCulture, PackageIdFormat, obj.Id.ToLowerInvariant(), obj.Version.ToNormalizedString().ToLowerInvariant()).GetHashCode();
        }
    }
}

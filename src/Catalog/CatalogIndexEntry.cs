// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog
{
    public sealed class CatalogIndexEntry : IComparable<CatalogIndexEntry>
    {
        private static readonly CatalogIndexEntryDateComparer _commitTimeStampComparer = new CatalogIndexEntryDateComparer();

        [JsonConstructor]
        private CatalogIndexEntry()
        {
            Types = Enumerable.Empty<string>();
        }

        public CatalogIndexEntry(Uri uri, string type, string commitId, DateTime commitTs, string id, NuGetVersion version)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));

            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(type));
            }

            Types = new[] { type };
            IsDelete = type == "nuget:PackageDelete";

            if (string.IsNullOrWhiteSpace(commitId))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(commitId));
            }

            CommitId = commitId;
            CommitTimeStamp = commitTs;

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(id));
            }

            Id = id;
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        [JsonProperty("@id")]
        [JsonRequired]
        public Uri Uri { get; private set; }

        [JsonProperty("@type")]
        [JsonRequired]
        [JsonConverter(typeof(CatalogTypeConverter))]
        public IEnumerable<string> Types { get; private set; }

        [JsonProperty("commitId")]
        [JsonRequired]
        public string CommitId { get; private set; }

        [JsonProperty("commitTimeStamp")]
        [JsonRequired]
        public DateTime CommitTimeStamp { get; private set; }

        [JsonProperty("nuget:id")]
        [JsonRequired]
        public string Id { get; private set; }

        [JsonProperty("nuget:version")]
        [JsonRequired]
        public NuGetVersion Version { get; private set; }

        [JsonIgnore]
        public bool IsDelete { get; }

        public int CompareTo(CatalogIndexEntry other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return _commitTimeStampComparer.Compare(this, other);
        }

        public static CatalogIndexEntry Create(JToken token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            var uri = new Uri(token["@id"].ToString());
            var type = token["@type"].ToString();
            var commitId = token["commitId"].ToString();
            var commitTimeStamp = DateTime.ParseExact(
                token["commitTimeStamp"].ToString(),
                "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ",
                DateTimeFormatInfo.CurrentInfo,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            var packageId = token["nuget:id"].ToString();
            var packageVersion = NuGetVersion.Parse(token["nuget:version"].ToString());

            return new CatalogIndexEntry(uri, type, commitId, commitTimeStamp, packageId, packageVersion);
        }
    }

    public class CatalogIndexEntryDateComparer : IComparer<CatalogIndexEntry>
    {
        public int Compare(CatalogIndexEntry x, CatalogIndexEntry y)
        {
            return x.CommitTimeStamp.CompareTo(y.CommitTimeStamp);
        }
    }
}
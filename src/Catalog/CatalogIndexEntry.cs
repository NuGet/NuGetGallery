// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog
{
    public sealed class CatalogIndexEntry : IComparable<CatalogIndexEntry>
    {
        [JsonConstructor]
        private CatalogIndexEntry()
        {
            Types = Enumerable.Empty<string>();
        }

        public CatalogIndexEntry(
            Uri uri,
            string type,
            string commitId,
            DateTime commitTs,
            PackageIdentity packageIdentity)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullEmptyOrWhitespace, nameof(type));
            }

            Initialize(uri, new[] { type }, commitId, commitTs, packageIdentity);
        }

        public CatalogIndexEntry(
            Uri uri,
            IReadOnlyList<string> types,
            string commitId,
            DateTime commitTs,
            PackageIdentity packageIdentity)
        {
            Initialize(uri, types, commitId, commitTs, packageIdentity);
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
        public bool IsDelete
        {
            get
            {
                return Types.Any(type => type == "nuget:PackageDelete");
            }
        }

        public int CompareTo(CatalogIndexEntry other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return CommitTimeStamp.CompareTo(other.CommitTimeStamp);
        }

        public static CatalogIndexEntry Create(CatalogCommitItem commitItem)
        {
            if (commitItem == null)
            {
                throw new ArgumentNullException(nameof(commitItem));
            }

            return new CatalogIndexEntry(
                commitItem.Uri,
                commitItem.Types,
                commitItem.CommitId,
                commitItem.CommitTimeStamp,
                commitItem.PackageIdentity);
        }

        private void Initialize(
            Uri uri,
            IReadOnlyList<string> types,
            string commitId,
            DateTime commitTs,
            PackageIdentity packageIdentity)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));

            if (types == null || !types.Any())
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(types));
            }

            if (types.Any(type => string.IsNullOrWhiteSpace(type)))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullEmptyOrWhitespace, nameof(types));
            }

            Types = types;

            if (string.IsNullOrWhiteSpace(commitId))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(commitId));
            }

            CommitId = commitId;
            CommitTimeStamp = commitTs;

            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            Id = packageIdentity.Id;
            Version = packageIdentity.Version;
        }
    }
}
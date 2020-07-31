// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Represents a single item in a catalog commit.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed class CatalogCommitItem : IComparable
    {
        private string DebuggerDisplay =>
            $"Catalog item {PackageIdentity?.Id} {PackageIdentity?.Version.ToNormalizedString()}" +
            $"{(IsPackageDetails ? " (" + nameof(Schema.DataTypes.PackageDetails) + ")" : string.Empty)}" +
            $"{(IsPackageDelete ? " (" + nameof(Schema.DataTypes.PackageDelete) + ")" : string.Empty)}";

        private const string _typeKeyword = "@type";

        public CatalogCommitItem(
            Uri uri,
            string commitId,
            DateTime commitTimeStamp,
            IReadOnlyList<string> types,
            IReadOnlyList<Uri> typeUris,
            PackageIdentity packageIdentity)
        {
            Uri = uri;
            CommitId = commitId;
            CommitTimeStamp = commitTimeStamp;
            PackageIdentity = packageIdentity;
            Types = types;
            TypeUris = typeUris;

            IsPackageDetails = HasTypeUri(Schema.DataTypes.PackageDetails);
            IsPackageDelete = HasTypeUri(Schema.DataTypes.PackageDelete);
        }

        public Uri Uri { get; }
        public DateTime CommitTimeStamp { get; }
        public string CommitId { get; }
        public PackageIdentity PackageIdentity { get; }
        public IReadOnlyList<string> Types { get; }
        public IReadOnlyList<Uri> TypeUris { get; }

        public bool IsPackageDetails { get; }
        public bool IsPackageDelete { get; }

        public bool HasTypeUri(Uri typeUri)
        {
            return TypeUris.Any(x => x.IsAbsoluteUri && x.AbsoluteUri == typeUri.AbsoluteUri);
        }

        public int CompareTo(object obj)
        {
            var other = obj as CatalogCommitItem;

            if (ReferenceEquals(other, null))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, Strings.ArgumentMustBeInstanceOfType, nameof(CatalogCommitItem)),
                    nameof(obj));
            }

            return CommitTimeStamp.CompareTo(other.CommitTimeStamp);
        }

        public static CatalogCommitItem Create(JObject context, JObject commitItem)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (commitItem == null)
            {
                throw new ArgumentNullException(nameof(commitItem));
            }

            var commitTimeStamp = Utils.Deserialize<DateTime>(commitItem, "commitTimeStamp");
            var commitId = Utils.Deserialize<string>(commitItem, "commitId");
            var idUri = Utils.Deserialize<Uri>(commitItem, "@id");
            var packageId = Utils.Deserialize<string>(commitItem, "nuget:id");
            var packageVersion = Utils.Deserialize<string>(commitItem, "nuget:version");
            var packageIdentity = new PackageIdentity(packageId, new NuGetVersion(packageVersion));
            var types = GetTypes(commitItem).ToArray();

            if (!types.Any())
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, Strings.NonEmptyPropertyValueRequired, _typeKeyword),
                    nameof(commitItem));
            }

            var typeUris = types.Select(type => Utils.Expand(context, type)).ToArray();

            return new CatalogCommitItem(idUri, commitId, commitTimeStamp, types, typeUris, packageIdentity);
        }

        private static IEnumerable<string> GetTypes(JObject commitItem)
        {
            if (commitItem.TryGetValue(_typeKeyword, out var value))
            {
                if (value is JArray)
                {
                    foreach (JToken typeToken in ((JArray)value).Values())
                    {
                        yield return typeToken.ToString();
                    }
                }
                else
                {
                    yield return value.ToString();
                }
            }
        }
    }
}
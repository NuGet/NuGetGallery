// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Represents a single catalog commit.
    /// </summary>
    public sealed class CatalogCommit : IComparable
    {
        private CatalogCommit(DateTime commitTimeStamp, Uri uri)
        {
            CommitTimeStamp = commitTimeStamp;
            Uri = uri;
        }

        public DateTime CommitTimeStamp { get; }
        public Uri Uri { get; }

        public int CompareTo(object obj)
        {
            var other = obj as CatalogCommit;

            if (ReferenceEquals(other, null))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, Strings.ArgumentMustBeInstanceOfType, nameof(CatalogCommit)),
                    nameof(obj));
            }

            return CommitTimeStamp.CompareTo(other.CommitTimeStamp);
        }

        public static CatalogCommit Create(JObject commit)
        {
            if (commit == null)
            {
                throw new ArgumentNullException(nameof(commit));
            }

            var commitTimeStamp = Utils.Deserialize<DateTime>(commit, "commitTimeStamp");
            var uri = Utils.Deserialize<Uri>(commit, "@id");

            return new CatalogCommit(commitTimeStamp, uri);
        }
    }
}
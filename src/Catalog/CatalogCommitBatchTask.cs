// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Represents an asynchrononous task associated with catalog changes for a specific commit item key
    /// and potentially spanning multiple commits.
    /// </summary>
    public sealed class CatalogCommitBatchTask : IEquatable<CatalogCommitBatchTask>
    {
        /// <summary>
        /// Initializes a <see cref="CatalogCommitItemBatch" /> instance.
        /// </summary>
        /// <param name="minCommitTimeStamp">The minimum commit timestamp for commit items with <paramref name="key" />.
        /// <param name="key">A unique key.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is <c>null</c>, empty,
        /// or whitespace.</exception>
        public CatalogCommitBatchTask(DateTime minCommitTimeStamp, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullEmptyOrWhitespace, nameof(key));
            }

            MinCommitTimeStamp = minCommitTimeStamp;
            Key = key;
        }

        public DateTime MinCommitTimeStamp { get; }
        public string Key { get; }
        public Task Task { get; set; }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CatalogCommitBatchTask);
        }

        public bool Equals(CatalogCommitBatchTask other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return string.Equals(Key, other.Key);
        }
    }
}
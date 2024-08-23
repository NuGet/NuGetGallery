// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Monitoring
{
    /// <summary>
    /// Fetches <see cref="PackageStatusOutdatedCheck"/>s from a source and maintains a <see cref="ReadWriteCursor"/> on it.
    /// </summary>
    public interface IPackageStatusOutdatedCheckSource
    {
        /// <summary>
        /// Fetches the next batch of <see cref="PackageStatusOutdatedCheck"/>s.
        /// </summary>
        /// <param name="top">Up to this many <see cref="PackageStatusOutdatedCheck"/>s are returned.</param>
        Task<IReadOnlyCollection<PackageStatusOutdatedCheck>> GetPackagesToCheckAsync(DateTime max, int top, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the <see cref="ReadWriteCursor"/> based on the last batch of packages returned by <see cref="GetPackagesToCheckAsync(int, CancellationToken)"/>.
        /// If <see cref="GetPackagesToCheckAsync(int, CancellationToken)"/> was never called, or the last batch was already processed, this method does nothing.
        /// </summary>
        Task MarkPackagesCheckedAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Moves back the <see cref="ReadWriteCursor"/> to a specific time.
        /// The next call to <see cref="GetPackagesToCheckAsync(int, CancellationToken)"/> will return the batches of packages after that time.
        /// If the cursor would be moving ahead, the change is not done.
        /// </summary>
        Task MoveBackAsync(DateTime value, CancellationToken cancellationToken);
    }

    public abstract class PackageStatusOutdatedCheckSource<T> : IPackageStatusOutdatedCheckSource
    {
        private readonly ReadWriteCursor _cursor;
        private DateTime? _pendingCursorValue;

        public PackageStatusOutdatedCheckSource(ReadWriteCursor cursor)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
        }

        public async Task<IReadOnlyCollection<PackageStatusOutdatedCheck>> GetPackagesToCheckAsync(DateTime max, int top, CancellationToken cancellationToken)
        {
            await _cursor.LoadAsync(cancellationToken);
            var packages = await GetPackagesToCheckAsync(_cursor.Value, max, top, cancellationToken);
            _pendingCursorValue = packages.Any() 
                ? packages.Max<T, DateTime>(GetCursorValue) 
                : (DateTime?)null;

            return packages
                .Select(GetPackageStatusOutdatedCheck)
                .ToList();
        }

        public async Task MarkPackagesCheckedAsync(CancellationToken cancellationToken)
        {
            if (!_pendingCursorValue.HasValue)
            {
                return;
            }

            await SetAsync(_pendingCursorValue.Value, cancellationToken);
            _pendingCursorValue = null;
        }

        public async Task MoveBackAsync(DateTime value, CancellationToken cancellationToken)
        {
            await _cursor.LoadAsync(cancellationToken);
            await SetAsync(new[] { _cursor.Value, value }.Min(), cancellationToken);
        }

        private Task SetAsync(DateTime value, CancellationToken cancellationToken)
        {
            _cursor.Value = value;
            return _cursor.SaveAsync(cancellationToken);
        }

        protected abstract DateTime GetCursorValue(T package);

        protected abstract PackageStatusOutdatedCheck GetPackageStatusOutdatedCheck(T package);

        protected abstract Task<IReadOnlyCollection<T>> GetPackagesToCheckAsync(
            DateTime since, DateTime max, int top, CancellationToken cancellationToken);
    }
}

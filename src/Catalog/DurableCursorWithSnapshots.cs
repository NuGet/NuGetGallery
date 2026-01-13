// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog
{
    public class DurableCursorWithSnapshots : DurableCursor
    {
        private readonly Uri _address;
        private readonly Persistence.Storage _storage;
        private readonly TimeSpan _minIntervalBetweenTwoSnapshots;
        private readonly int _maxNumberOfSnapshotsToKeep;

        private DateTime _latestTimestampOfTakingSnapshotInUtc;
        private IList<Snapshot> _snapshots;

        public DurableCursorWithSnapshots(Uri address, Persistence.Storage storage, DateTime defaultValue,
            TimeSpan minIntervalBetweenTwoSnapshots, int maxNumberOfSnapshotsToKeep)
            : base(address, storage, defaultValue)
        {
            _address = address;
            _storage = storage;
            _minIntervalBetweenTwoSnapshots = minIntervalBetweenTwoSnapshots;
            _maxNumberOfSnapshotsToKeep = maxNumberOfSnapshotsToKeep;

            _latestTimestampOfTakingSnapshotInUtc = DateTime.MinValue;
            _snapshots = new List<Snapshot>();
        }

        public override async Task SaveAsync(CancellationToken cancellationToken)
        {
            await base.SaveAsync(cancellationToken);

            await UpdateSnapshots(cancellationToken);
        }

        private async Task UpdateSnapshots(CancellationToken cancellationToken)
        {
            if (_snapshots.Count == 0)
            {
                await LoadSnapshots(cancellationToken);
            }

            if (DateTime.UtcNow - _latestTimestampOfTakingSnapshotInUtc < _minIntervalBetweenTwoSnapshots)
            {
                Trace.TraceInformation("The latest snapshot of Cursor: {0} was taken at {1}. No snapshot is created as the minimal interval between two snapshots is {2} seconds.",
                    _address.AbsoluteUri, _latestTimestampOfTakingSnapshotInUtc.ToString("O"), _minIntervalBetweenTwoSnapshots.TotalSeconds.ToString());

                return;
            }

            Trace.TraceInformation("Creating the snapshot of Cursor: {0}.", _address.AbsoluteUri);

            var snapshot = await _storage.CreateSnapshotAsync(_address, cancellationToken);
            _snapshots.Add(snapshot);
            _latestTimestampOfTakingSnapshotInUtc = snapshot.CreatedTimestampInUtc;

            Trace.TraceInformation("Created the snapshot of Cursor: {0}.", _address.AbsoluteUri);

            while (_snapshots.Count > _maxNumberOfSnapshotsToKeep)
            {
                var snapshotToDelete = _snapshots[0];

                Trace.TraceInformation("Deleting the snapshot of Cursor: {0} taken at {1}. There are {2} snapshots.",
                    _address.AbsoluteUri, snapshotToDelete.CreatedTimestampInUtc.ToString("O"), _snapshots.Count);

                try
                {
                    await _storage.DeleteSnapshotAsync(_address, snapshotToDelete, cancellationToken);
                    _snapshots.RemoveAt(0);
                }
                catch (RequestFailedException e) when (e.Status == (int) HttpStatusCode.NotFound)
                {
                    Trace.TraceInformation("The snapshot of Cursor: {0} taken at {1} was not found. Reload snapshots.",
                        _address.AbsoluteUri, snapshotToDelete.CreatedTimestampInUtc.ToString("O"), _snapshots.Count);

                    await LoadSnapshots(cancellationToken);
                }

                Trace.TraceInformation("Deleted the snapshot of Cursor: {0} taken at {1}. There are {2} snapshots.",
                    _address.AbsoluteUri, snapshotToDelete.CreatedTimestampInUtc.ToString("O"), _snapshots.Count);
            }
        }

        private async Task LoadSnapshots(CancellationToken cancellationToken)
        {
            Trace.TraceInformation("Loading snapshots of Cursor: {0}", _address.AbsoluteUri);

            var snapshots = await _storage.ListSnapshotsAsync(_address, cancellationToken);

            _snapshots = snapshots.OrderBy(s => s.CreatedTimestampInUtc).ToList();
            if (_snapshots.Count > 0)
            {
                _latestTimestampOfTakingSnapshotInUtc = _snapshots.Last().CreatedTimestampInUtc;
            }

            Trace.TraceInformation("Loaded snapshots of Cursor: {0}. There are {1} snapshots and the latest snapshot was taken at {2}.",
                _address.AbsoluteUri, _snapshots.Count, _latestTimestampOfTakingSnapshotInUtc.ToString("O"));
        }
    }
}

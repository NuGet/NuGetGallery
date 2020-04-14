// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch
{
    public class PackageIdToPopularityTransfersBuilder
    {
        private readonly ILogger _logger;
        private int _addCount;
        private readonly Dictionary<string, string> _idInternPool;
        private readonly SortedDictionary<string, SortedSet<string>> _result;

        public PackageIdToPopularityTransfersBuilder(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _addCount = 0;
            _idInternPool = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _result = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Add multiple popularity transfers.
        /// </summary>
        /// <param name="fromId">The package that is transferring its popularity away.</param>
        /// <param name="toIds">The packages that are receiving the transferred popularity.</param>
        public void Add(string fromId, IReadOnlyList<string> toIds)
        {
            foreach (var toId in toIds)
            {
                Add(fromId, toId);
            }
        }

        /// <summary>
        /// Add a popularity transfers
        /// </summary>
        /// <param name="fromId">The package that is transferring its popularity away.</param>
        /// <param name="toId">The package that is receiving the transferred popularity.</param>
        public void Add(string fromId, string toId)
        {
            _addCount++;
            if (_addCount % 10000 == 0)
            {
                _logger.LogInformation("{AddCount} popularity transfers have been added so far.", _addCount);
            }

            // Use a single instance of each "toId" string.
            toId = InternId(toId);

            if (!_result.TryGetValue(fromId, out var toIds))
            {
                toIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                fromId = InternId(fromId);

                _result.Add(fromId, toIds);
            }

            toIds.Add(toId);
        }

        /// <summary>
        /// Get the popularity transfers.
        /// </summary>
        /// <returns>A map of packages transferring popularity away to the packages receiving the popularity.</returns>
        public SortedDictionary<string, SortedSet<string>> GetResult()
        {
            _logger.LogInformation("{RecordCount} popularity transfers were found.", _addCount);
            _logger.LogInformation("{FromTransfers} packages transfer popularity away.", _result.Count);
            _logger.LogInformation("{UniqueIds} unique package IDs.", _idInternPool.Count);

            return _result;
        }

        private string InternId(string id)
        {
            if (_idInternPool.TryGetValue(id, out var existingId))
            {
                return existingId;
            }

            _idInternPool.Add(id, id);
            return id;
        }
    }
}

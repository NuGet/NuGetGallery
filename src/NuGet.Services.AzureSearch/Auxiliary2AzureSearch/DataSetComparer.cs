// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class DataSetComparer : IDataSetComparer
    {
        private static readonly string[] EmptyStringArray = new string[0];

        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<DataSetComparer> _logger;

        public DataSetComparer(
            IAzureSearchTelemetryService telemetryService,
            ILogger<DataSetComparer> logger)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public SortedDictionary<string, string[]> CompareOwners(
            SortedDictionary<string, SortedSet<string>> oldData,
            SortedDictionary<string, SortedSet<string>> newData)
        {
            // Use ordinal comparison to allow username case changes to flow through.
            var stopwatch = Stopwatch.StartNew();
            var result = CompareData(
                oldData,
                newData,
                "package ID",
                "owners",
                StringComparer.Ordinal);

            stopwatch.Stop();
            _telemetryService.TrackOwnerSetComparison(oldData.Count, newData.Count, result.Count, stopwatch.Elapsed);

            return result;
        }

        public SortedDictionary<string, string[]> ComparePopularityTransfers(
            SortedDictionary<string, SortedSet<string>> oldData,
            SortedDictionary<string, SortedSet<string>> newData)
        {
            // Ignore case changes in popularity transfers.
            var stopwatch = Stopwatch.StartNew();
            var result = CompareData(
                oldData,
                newData,
                "package ID",
                "popularity transfers",
                StringComparer.OrdinalIgnoreCase);

            stopwatch.Stop();
            _telemetryService.TrackPopularityTransfersSetComparison(oldData.Count, newData.Count, result.Count, stopwatch.Elapsed);

            return result;
        }

        private SortedDictionary<string, string[]> CompareData(
            SortedDictionary<string, SortedSet<string>> oldData,
            SortedDictionary<string, SortedSet<string>> newData,
            string keyName,
            string valuesName,
            StringComparer valuesComparer)
        {
            if (oldData.Comparer != StringComparer.OrdinalIgnoreCase)
            {
                throw new ArgumentException("The old data should have a case-insensitive comparer.", nameof(oldData));
            }

            if (newData.Comparer != StringComparer.OrdinalIgnoreCase)
            {
                throw new ArgumentException("The new data should have a case-insensitive comparer.", nameof(newData));
            }

            // We use a very simplistic algorithm here. Perform one pass on the new data to find the added or changed
            // values. Then perform a second pass on the old data to find removed keys. We can optimize
            // this later if necessary.
            //
            // On the "changed" case, we emit all of the values instead of the delta. This is because Azure Search
            // does not have a way to append or remove a specific item from a field that is an array.
            // The entire new array needs to be provided.
            var result = new SortedDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            // First pass: find added or changed sets.
            foreach (var pair in newData)
            {
                var key = pair.Key;
                var newValues = pair.Value;
                if (!oldData.TryGetValue(key, out var oldValues))
                {
                    // ADDED: The key does not exist in the old data, which means the key was added.
                    result.Add(key, newValues.ToArray());
                    _logger.LogInformation(
                        $"The {keyName} {{Key}} has been added, with {{AddedCount}} {valuesName}.",
                        key,
                        newValues.Count);
                }
                else
                {
                    // The key exists in the old data. We need to check if the values set has changed.
                    var removedValues = oldValues.Except(newValues, valuesComparer).ToList();
                    var addedValues = newValues.Except(oldValues, valuesComparer).ToList();

                    if (removedValues.Any() || addedValues.Any())
                    {
                        // CHANGED: The values set has changed.
                        result.Add(key, newValues.ToArray());
                        _logger.LogInformation(
                            $"The {keyName} {{Key}} {valuesName} have changed, with {{RemovedCount}} {valuesName} removed and " +
                            $"{{AddedCount}} {valuesName} added.",
                            key,
                            removedValues.Count,
                            addedValues.Count);
                    }
                }
            }

            // Second pass: find removed sets.
            foreach (var pair in oldData)
            {
                var key = pair.Key;
                var oldValues = pair.Value;

                if (!newData.TryGetValue(key, out var newValues))
                {
                    // REMOVED: The key does not exist in the new data, which means the key was removed.
                    result.Add(key, EmptyStringArray);
                    _logger.LogInformation(
                        $"The {keyName} {{Key}} has been removed, with {{RemovedCount}} {valuesName}",
                        key,
                        oldValues.Count);
                }
            }

            return result;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public class OwnerSetComparer : IOwnerSetComparer
    {
        private static readonly string[] EmptyStringArray = new string[0];

        private readonly ILogger _logger;

        public OwnerSetComparer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public SortedDictionary<string, string[]> Compare(
            SortedDictionary<string, SortedSet<string>> oldData,
            SortedDictionary<string, SortedSet<string>> newData)
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
            // package IDs. Then perform a second pass on the old data to find removed package IDs. We can optimize
            // this later if necessary.
            //
            // We emit all of the usernames when a package ID's owners have changed instead of the delta. This is
            // because Azure Search does not have a way to append or remove a specific item from a field that is an
            // array. The entire new array needs to be provided.
            var result = new SortedDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            // First pass: find added or changed sets.
            foreach (var pair in newData)
            {
                var id = pair.Key;
                var newOwners = pair.Value;
                if (!oldData.TryGetValue(id, out var oldOwners))
                {
                    // ADDED: The package ID does not exist in the old data, which means the package ID was added.
                    result.Add(id, newOwners.ToArray());
                    _logger.LogInformation(
                        "The package ID {ID} has been added, with {AddedCount} owners.",
                        id,
                        newOwners.Count);
                }
                else
                {
                    // The package ID exists in the old data. We need to check if the owner set has changed. Perform
                    // an ordinal comparison to allow username case changes to flow through.
                    var removedUsernames = oldOwners.Except(newOwners, StringComparer.Ordinal).ToList();
                    var addedUsernames = newOwners.Except(oldOwners, StringComparer.Ordinal).ToList();

                    if (removedUsernames.Any() || addedUsernames.Any())
                    {
                        // CHANGED: The username set has changed.
                        result.Add(id, newOwners.ToArray());
                        _logger.LogInformation(
                            "The package ID {ID} has an ownership change, with {RemovedCount} owners removed and " +
                            "{AddedCount} owners added.",
                            id,
                            removedUsernames.Count,
                            addedUsernames.Count);
                    }
                }
            }

            // Second pass: find removed sets.
            foreach (var pair in oldData)
            {
                var id = pair.Key;
                var oldOwners = pair.Value;

                if (!newData.TryGetValue(id, out var newOwners))
                {
                    // REMOVED: The package ID does not exist in the new data, which means the package ID was removed.
                    result.Add(id, EmptyStringArray);
                    _logger.LogInformation(
                        "The package ID {ID} has been removed, with {RemovedCount} owners",
                        id,
                        oldOwners.Count);
                }
            }

            return result;
        }
    }
}


// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public class PackageIdToOwnersBuilder
    {
        private readonly ILogger _logger;
        private int _addCount;
        private readonly Dictionary<string, string> _usernameToUsername;
        private readonly SortedDictionary<string, SortedSet<string>> _result;

        public PackageIdToOwnersBuilder(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _addCount = 0;
            _usernameToUsername = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _result = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        public void Add(string id, IReadOnlyList<string> owners)
        {
            foreach (var username in owners)
            {
                Add(id, username);
            }
        }

        public void Add(string id, string username)
        {
            _addCount++;
            if (_addCount % 10000 == 0)
            {
                _logger.LogInformation("{AddCount} ownership records have been added so far.", _addCount);
            }

            // Use a single instance of each username string.
            if (!_usernameToUsername.TryGetValue(username, out var existingUsername))
            {
                _usernameToUsername.Add(username, username);
            }
            else
            {
                username = existingUsername;
            }

            if (!_result.TryGetValue(id, out var owners))
            {
                owners = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                _result.Add(id, owners);
            }

            owners.Add(username);
        }

        public SortedDictionary<string, SortedSet<string>> GetResult()
        {
            _logger.LogInformation("{RecordCount} ownership records were found.", _addCount);
            _logger.LogInformation("{UsernameCount} usernames were found.", _usernameToUsername.Count);
            _logger.LogInformation("{IdCount} package IDs were found.", _result.Count);

            return _result;
        }
    }
}


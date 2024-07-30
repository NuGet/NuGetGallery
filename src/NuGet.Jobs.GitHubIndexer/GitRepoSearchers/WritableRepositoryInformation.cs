// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    /// <summary>
    /// Builder class to construct a RepositoryInformation
    /// </summary>
    public class WritableRepositoryInformation
    {
        private readonly HashSet<string> _writableDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Using a HashSet to avoid duplicates

        public WritableRepositoryInformation(string id, string url, int stars, string description, string mainBranch)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Url = url ?? throw new ArgumentNullException(nameof(url));
            Stars = stars;
            MainBranch = mainBranch ?? throw new ArgumentNullException(nameof(mainBranch));
        }

        public string Id { get; }
        public string Description { get; }
        public string Url { get; }
        public int Stars { get; }
        public string MainBranch { get; }

        /// <summary>
        /// Adds the specified dependency if it's not already present
        /// </summary>
        /// <param name="dependency">Dependency to add</param>
        public void AddDependency(string dependency)
        {
            _writableDependencies.Add(dependency);
        }

        /// <summary>
        /// Adds the dependencies by ignoring duplicates
        /// </summary>
        /// <param name="dependencies">Dependencies to add</param>
        public void AddDependencies(IEnumerable<string> dependencies)
        {
            foreach (var elem in dependencies)
            {
                _writableDependencies.Add(elem);
            }
        }

        public RepositoryInformation ToRepositoryInformation()
        {
            return new RepositoryInformation(Id, Url, Stars, Description, _writableDependencies.ToList());
        }
    }
}

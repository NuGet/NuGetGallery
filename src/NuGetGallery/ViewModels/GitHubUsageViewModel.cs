// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class GitHubUsageViewModel
    {
        public GitHubUsageViewModel(string comparableGitHubRepository, NuGetPackageGitHubInformation gitHubUsage)
        {
            TotalRepos = gitHubUsage.TotalRepos;

            // filter out repositories that are not the same as the package itself
            var repos = new List<RepositoryInformation>();
            foreach (var repo in gitHubUsage.Repos)
            {
                if (repo.Id.Equals(comparableGitHubRepository, StringComparison.OrdinalIgnoreCase))
                {
                    TotalRepos--;
                    continue;
                }

                repos.Add(repo);

                if (repos.Count == NuGetPackageGitHubInformation.ReposPerPackage - 1)
                {
                    break;
                }
            }

            Repos = repos;
        }

        public int TotalRepos { get; }
        public IReadOnlyList<RepositoryInformation> Repos { get; }
    }
}

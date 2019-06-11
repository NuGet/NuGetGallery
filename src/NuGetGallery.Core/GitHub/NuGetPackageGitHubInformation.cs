// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class NuGetPackageGitHubInformation
    {
        public const int ReposPerPackage = 10;

        public readonly static NuGetPackageGitHubInformation Empty = new NuGetPackageGitHubInformation(new List<RepositoryInformation>());

        public NuGetPackageGitHubInformation(IReadOnlyList<RepositoryInformation> repos)
        {
            if( repos == null)
            {
                throw new ArgumentNullException(nameof(repos));
            }

            TotalRepos = repos.Count;
            Repos = repos
                .OrderByDescending(x => x.Stars)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Take(ReposPerPackage)
                .ToList();
        }

        public int TotalRepos { get; }
        public IReadOnlyList<RepositoryInformation> Repos { get; }
    }
}

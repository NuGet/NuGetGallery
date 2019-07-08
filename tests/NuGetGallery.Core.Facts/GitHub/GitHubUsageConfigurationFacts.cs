﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery
{
    public class GitHubUsageConfigurationFacts
    {
        private static RepositoryInformation CreateRepo(string id, int stars = 100, params string[] nugetDependencies)
        {
            return new RepositoryInformation(
                id,
                "http://randomUrl.fake",
                stars,
                "Some random repository description",
                nugetDependencies);
        }

        private static GitHubUsageConfiguration GenConfig(params RepositoryInformation[] repos)
        {
            return new GitHubUsageConfiguration(repos);
        }

        public class TheConstructor
        {
            [Fact]
            public void InvalidRepoCache()
            {
                Assert.Throws<ArgumentNullException>(() => new GitHubUsageConfiguration(null));
            }
        }

        public class TheGetPackageInformationMethod
        {
            [Fact]
            public void EmptyRepoCache()
            {
                var gh = new GitHubUsageConfiguration(new List<RepositoryInformation>());
                Assert.Equal(NuGetPackageGitHubInformation.Empty, gh.GetPackageInformation("randomPkg"));
            }

            [Fact]
            public void NullPackageId()
            {
                var gh = new GitHubUsageConfiguration(new List<RepositoryInformation>());
                Assert.Throws<ArgumentNullException>(() => gh.GetPackageInformation(null));
            }

            [Fact]
            public void SingleRepoOneDependency()
            {
                var expectedRepo = CreateRepo("owner/id1", 1, "nupkg1");
                var gh = GenConfig(expectedRepo);
                var nupkgInformation = gh.GetPackageInformation("nupkg1");

                Assert.Equal(1, nupkgInformation.Repos.Count);
                Assert.Equal(1, nupkgInformation.TotalRepos);
                Assert.Equal(expectedRepo, nupkgInformation.Repos.First());
            }

            [Fact]
            public void MultiReposSameDependency()
            {
                var expectedRepo1 = CreateRepo("owner/B", 1, "nupkg1");
                var expectedRepo2 = CreateRepo("owner/A", 1, "nupkg1");
                var gh = GenConfig(expectedRepo1, expectedRepo2);
                var nupkgInformation = gh.GetPackageInformation("nupkg1");

                Assert.Equal(2, nupkgInformation.Repos.Count);
                Assert.Equal(2, nupkgInformation.TotalRepos);

                // Make sure they're alphabetically ordered since they have the same star count!
                Assert.Equal(expectedRepo2, nupkgInformation.Repos.First());
                Assert.Equal(expectedRepo1, nupkgInformation.Repos.ToList()[1]);
            }

            [Fact]
            public void OneRepoMultiDependencies()
            {
                var expectedRepo = CreateRepo("owner/A", 1, "nupkg1", "nupkg2");
                var gh = GenConfig(expectedRepo);
                var nupkgInformation1 = gh.GetPackageInformation("nupkg1");
                var nupkgInformation2 = gh.GetPackageInformation("nupkg2");

                Assert.Equal(1, nupkgInformation1.Repos.Count);
                Assert.Equal(1, nupkgInformation1.TotalRepos);
                Assert.Equal(expectedRepo, nupkgInformation1.Repos.First());

                Assert.Equal(1, nupkgInformation2.Repos.Count);
                Assert.Equal(1, nupkgInformation2.TotalRepos);
                Assert.Equal(expectedRepo, nupkgInformation2.Repos.First());
            }

            [Fact]
            public void MultiReposSameDependencyOrderedByStarCount()
            {
                RepositoryInformation[] expectedRepos =
                {
                    CreateRepo("owner/A",  1, "nupkg1"),
                    CreateRepo("owner/B",  2, "nupkg1"),
                    CreateRepo("owner/C",  3, "nupkg1"),
                    CreateRepo("owner/D",  4, "nupkg1"),
                    CreateRepo("owner/E",  5, "nupkg1")
                };

                var gh = GenConfig(expectedRepos);
                var nupkgInformation = gh.GetPackageInformation("nupkg1");

                Assert.Equal(Math.Min(expectedRepos.Length, NuGetPackageGitHubInformation.ReposPerPackage), nupkgInformation.Repos.Count);
                Assert.Equal(expectedRepos.Length, nupkgInformation.TotalRepos);

                // Make sure they're ordered by descending order of stars
                int lastStarCount = int.MaxValue;
                foreach (var repo in nupkgInformation.Repos)
                {
                    Assert.True(repo.Stars <= lastStarCount);
                    lastStarCount = repo.Stars;
                }
            }

            [Fact]
            public void OrderedByStarCountThenByIdThenTrimmed()
            {
                RepositoryInformation[] expectedRepos =
                {
                    CreateRepo("owner/A",  1, "nupkg1"),
                    CreateRepo("owner/B",  2, "nupkg1"),
                    CreateRepo("owner/C",  3, "nupkg1"),
                    CreateRepo("owner/D",  5, "nupkg1"),
                    CreateRepo("owner/E",  5, "nupkg1"),
                    CreateRepo("owner/F",  7, "nupkg1"),
                    CreateRepo("owner/G",  7, "nupkg1"),
                    CreateRepo("owner/H",  7, "nupkg1"),
                    CreateRepo("owner/I",  8, "nupkg1"),
                    CreateRepo("owner/J",  9, "nupkg1"),
                    CreateRepo("owner/K",  1, "nupkg1"),
                    CreateRepo("owner/L",  10, "nupkg1")
                };

                var gh = GenConfig(expectedRepos);
                var nupkgInformation = gh.GetPackageInformation("nupkg1");

                Assert.Equal(Math.Min(expectedRepos.Length, NuGetPackageGitHubInformation.ReposPerPackage), nupkgInformation.Repos.Count);
                Assert.Equal(expectedRepos.Length, nupkgInformation.TotalRepos);

                Assert.Equal(
                    expectedRepos
                        .OrderByDescending(x => x.Stars)
                        .ThenBy(x => x.Id)
                        .Take(nupkgInformation.Repos.Count),
                    nupkgInformation.Repos);
            }

            [Fact]
            public void CaseInsensitive()
            {
                var expectedRepo = CreateRepo("owner/A", 1, "nupkg1");
                var gh = GenConfig(expectedRepo);
                var nupkgInformation = gh.GetPackageInformation("NuPkG1");

                Assert.Equal(1, nupkgInformation.Repos.Count);
                Assert.Equal(1, nupkgInformation.TotalRepos);
                Assert.Equal(expectedRepo, nupkgInformation.Repos.First());
            }
        }
    }
}

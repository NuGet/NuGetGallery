// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGetGallery.Services;
using Microsoft.Web.XmlTransform;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging.Abstractions;
using System.Web.UI.WebControls;
using System.Collections;
using System.Diagnostics;

namespace NuGetGallery
{
    public static class Extensions
    {
        public static string KiloFormat(this int number)
        {
            if (number >= 1_000_000_000)
                return new System.Text.StringBuilder((number / 1_000_000_000.0f).ToString("F3")) { [4] = 'G' }.ToString();
            if (number >= 100_000_000)
                return (number / 1000) + "M";
            if (number >= 10_000_000)
                return new System.Text.StringBuilder((number / 1000000.0f).ToString("F2")) { [4] = 'M' }.ToString();
            if (number >= 1_000_000)
                return new System.Text.StringBuilder((number / 1000000.0f).ToString("F3")) { [4] = 'M' }.ToString();
            if (number >= 100_000)
                return (number / 1000) + "K";
            if (number >= 10_000)
                return new System.Text.StringBuilder((number / 1000.0f).ToString("F2")) { [4] = 'K' }.ToString();
            if (number >= 1000)
                return new System.Text.StringBuilder((number / 1000.0f).ToString("F3")) { [4] = 'K' }.ToString();

            return number.ToString("#,0");
        }
    }

    public class ContentObjectService : IContentObjectService
    {
        public static TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

        private readonly IContentService _contentService;

        public ContentObjectService(IContentService contentService)
        {
            _contentService = contentService;

            LoginDiscontinuationConfiguration = new LoginDiscontinuationConfiguration();
            CertificatesConfiguration = new CertificatesConfiguration();
            SymbolsConfiguration = new SymbolsConfiguration();
            TyposquattingConfiguration = new TyposquattingConfiguration();
        }

        public ILoginDiscontinuationConfiguration LoginDiscontinuationConfiguration { get; set; }
        public ICertificatesConfiguration CertificatesConfiguration { get; set; }
        public ISymbolsConfiguration SymbolsConfiguration { get; set; }
        public ITyposquattingConfiguration TyposquattingConfiguration { get; set; }
        public Dictionary<string, NuGetPackageInformation> NuGetPackagesGitHubDependencies { get; set; }

        public async Task Refresh()
        {
            LoginDiscontinuationConfiguration = 
                await Refresh<LoginDiscontinuationConfiguration>(GalleryConstants.ContentNames.LoginDiscontinuationConfiguration) ??
                new LoginDiscontinuationConfiguration();

            CertificatesConfiguration =
                await Refresh<CertificatesConfiguration>(GalleryConstants.ContentNames.CertificatesConfiguration) ??
                new CertificatesConfiguration();

            SymbolsConfiguration =
                await Refresh<SymbolsConfiguration>(GalleryConstants.ContentNames.SymbolsConfiguration) ??
                new SymbolsConfiguration();

            TyposquattingConfiguration =
               await Refresh<TyposquattingConfiguration>(GalleryConstants.ContentNames.TyposquattingConfiguration) ??
               new TyposquattingConfiguration();

            IReadOnlyList<RepositoryInformation> reposCache = await Refresh<IReadOnlyList<RepositoryInformation>>(
                    GalleryConstants.ContentNames.NuGetPackagesGitHubDependencies) ??
               Array.Empty<RepositoryInformation>();

            var tempDict = new Dictionary<string, List<RepositoryInformation>>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var repo in reposCache)
            {
                foreach (var dependency in repo.Dependencies)
                {
                    List<RepositoryInformation> nuGetPackageInformation = null;
                    if (tempDict.ContainsKey(dependency))
                    {
                        nuGetPackageInformation = tempDict[dependency];
                    }
                    else
                    {
                        nuGetPackageInformation = new List<RepositoryInformation>();
                    }
                    nuGetPackageInformation.Add(repo);
                    tempDict[dependency] = nuGetPackageInformation;
                }
            }

            var tempSwapDict = new Dictionary<string, NuGetPackageInformation>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var entry in tempDict)
            {
                entry.Value.Sort(Comparer<RepositoryInformation>.Create((x, y) =>
                {
                    var result = y.CompareTo(x); // Inverted for descending sort order
                    if (result != 0)
                    {
                        return result;
                    }

                    // Results have the same star count, compare their ids (not inverted) to sort in alphabetical order
                    return string.Compare(x.Id, y.Id, true);
                }));

                var nuGetPackageInformation = new NuGetPackageInformation();
                nuGetPackageInformation.TotalRepos = entry.Value.Count;
                nuGetPackageInformation.Repos = entry.Value.Take(10).ToList().AsReadOnly();
                tempSwapDict[entry.Key] = nuGetPackageInformation;
            }

            // This is done to avoid the Concurent read & modification of the dictionary
            NuGetPackagesGitHubDependencies = tempSwapDict;
        }

        private async Task<T> Refresh<T>(string contentName) 
            where T : class
        {
            var configString = (await _contentService.GetContentItemAsync(contentName, RefreshInterval))?.ToString();
            if (string.IsNullOrEmpty(configString))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<T>(configString);
        }

        public class NuGetPackageInformation
        {
            public int TotalRepos { get; set; }
            public IReadOnlyList<RepositoryInformation> Repos { get; set; }

            public NuGetPackageInformation()
            {
                TotalRepos = 0;
                Repos = null;
            }
        }

        public class RepositoryInformation : IEquatable<RepositoryInformation>, IComparable<RepositoryInformation>
        {
            [JsonIgnore]
            public string Name { get; set; }
            [JsonIgnore]
            public string Owner { get; set; }
            public string Url { get; set; }
            public int Stars { get; set; }
            public string Id
            {
                get => Owner + "/" + Name; set
                {
                    var split = value.Split('/');
                    if (split.Length == 2)
                    {
                        Owner = split[0];
                        Name = split[1];
                    }
                }
            }

            public List<string> Dependencies { get; set; } = null;

            public RepositoryInformation()
            { }

            public RepositoryInformation(string owner, string repoName, string cloneUrl, int starCount, List<string> dependencies)
            {
                Owner = owner;
                Name = repoName;
                Url = cloneUrl;
                Stars = starCount;
                Dependencies = dependencies;
            }

            public override bool Equals(object obj)
            {
                return obj is RepositoryInformation information && Equals(information);
            }

            public bool Equals(RepositoryInformation other)
            {
                return Url.Equals(other.Url, StringComparison.InvariantCultureIgnoreCase);
            }
            public override int GetHashCode()
            {
                // Using toLower() to make the hash case insensitive
                return Url.ToLower().GetHashCode();
            }

            public int CompareTo(RepositoryInformation other)
            {
                return Stars.CompareTo(other.Stars);
            }
        }
    }
}
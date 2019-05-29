// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGetGallery.Services;

namespace NuGetGallery
{
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
        public Dictionary<string, SortedSet<RepositoryInformation>> NuGetPackagesGitHubDependencies { get; set; }

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

            NuGetPackagesGitHubDependencies =
                await Refresh<Dictionary<string, SortedSet<RepositoryInformation>>>(
                    GalleryConstants.ContentNames.NuGetPackagesGitHubDependencies) ??
               new Dictionary<string, SortedSet<RepositoryInformation>>();
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


        public struct RepositoryInformation : IEquatable<RepositoryInformation>, IComparable<RepositoryInformation>
        {
            public string Name { get; set; }
            public string Owner { get; set; }
            public string CloneUrl { get; set; }
            public int Stars { get; set; }

            public string FullName
            {
                get => Owner + "/" + Name;
                set
                {
                    var split = value.Split('/');
                    if (split.Length == 2)
                    {
                        Owner = split[0];
                        Name = split[1];
                    }
                }
            }

            public RepositoryInformation(string owner, string repoName, string cloneUrl, int starCount)
            {
                Owner = owner;
                Name = repoName;
                CloneUrl = cloneUrl;
                Stars = starCount;
            }

            public override bool Equals(object obj)
            {
                return obj is RepositoryInformation information && Equals(information);
            }

            public bool Equals(RepositoryInformation other)
            {
                return CloneUrl.Equals(other.CloneUrl, StringComparison.InvariantCultureIgnoreCase);
            }
            public override int GetHashCode()
            {
                // Using toLower() to make the hash case insensitive
                return CloneUrl.ToLower().GetHashCode();
            }

            public int CompareTo(RepositoryInformation other)
            {
                // It is inverted here so the Repos would always be sorted from high starCount to low starCount
                return other.Stars.CompareTo(Stars);
            }
        }
    }
}
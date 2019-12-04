// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            GitHubUsageConfiguration = new GitHubUsageConfiguration(Array.Empty<RepositoryInformation>());
            ABTestConfiguration = new ABTestConfiguration();
            ODataCacheConfiguration = new ODataCacheConfiguration();
        }

        public ILoginDiscontinuationConfiguration LoginDiscontinuationConfiguration { get; private set; }
        public ICertificatesConfiguration CertificatesConfiguration { get; private set; }
        public ISymbolsConfiguration SymbolsConfiguration { get; private set; }
        public ITyposquattingConfiguration TyposquattingConfiguration { get; private set; }
        public IGitHubUsageConfiguration GitHubUsageConfiguration { get; private set; }
        public IABTestConfiguration ABTestConfiguration { get; private set; }
        public IODataCacheConfiguration ODataCacheConfiguration { get; private set; }

        public async Task Refresh()
        {
            LoginDiscontinuationConfiguration = 
                await Refresh<LoginDiscontinuationConfiguration>(ServicesConstants.ContentNames.LoginDiscontinuationConfiguration) ??
                new LoginDiscontinuationConfiguration();

            CertificatesConfiguration =
                await Refresh<CertificatesConfiguration>(ServicesConstants.ContentNames.CertificatesConfiguration) ??
                new CertificatesConfiguration();

            SymbolsConfiguration =
                await Refresh<SymbolsConfiguration>(ServicesConstants.ContentNames.SymbolsConfiguration) ??
                new SymbolsConfiguration();

            TyposquattingConfiguration =
               await Refresh<TyposquattingConfiguration>(ServicesConstants.ContentNames.TyposquattingConfiguration) ??
               new TyposquattingConfiguration();

            var reposCache = 
                await Refresh<IReadOnlyCollection<RepositoryInformation>>(ServicesConstants.ContentNames.NuGetPackagesGitHubDependencies) ??
                Array.Empty<RepositoryInformation>();
            GitHubUsageConfiguration = new GitHubUsageConfiguration(reposCache);

            ABTestConfiguration =
               await Refresh<ABTestConfiguration>(ServicesConstants.ContentNames.ABTestConfiguration) ??
               new ABTestConfiguration();

            ODataCacheConfiguration =
               await Refresh<ODataCacheConfiguration>(ServicesConstants.ContentNames.ODataCacheConfiguration) ??
               new ODataCacheConfiguration();
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
    }
}
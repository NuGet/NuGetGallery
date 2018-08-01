// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public class ContentObjectService : IContentObjectService
    {
        private const int RefreshIntervalMinutes = 5;

        private readonly IContentService _contentService;

        public ContentObjectService(IContentService contentService)
        {
            _contentService = contentService;

            LoginDiscontinuationConfiguration = new LoginDiscontinuationConfiguration();
            CertificatesConfiguration = new CertificatesConfiguration();
            SymbolsConfiguration = new SymbolsConfiguration();
        }

        public ILoginDiscontinuationConfiguration LoginDiscontinuationConfiguration { get; set; }
        public ICertificatesConfiguration CertificatesConfiguration { get; set; }
        public ISymbolsConfiguration SymbolsConfiguration { get; set; }

        public async Task Refresh()
        {
            LoginDiscontinuationConfiguration = 
                await Refresh<LoginDiscontinuationConfiguration>(Constants.ContentNames.LoginDiscontinuationConfiguration) ??
                new LoginDiscontinuationConfiguration();

            CertificatesConfiguration =
                await Refresh<CertificatesConfiguration>(Constants.ContentNames.CertificatesConfiguration) ??
                new CertificatesConfiguration();

            SymbolsConfiguration =
                await Refresh<SymbolsConfiguration>(Constants.ContentNames.SymbolsConfiguration) ??
                new SymbolsConfiguration();
        }

        private async Task<T> Refresh<T>(string contentName) 
            where T : class
        {
            var configString = (await _contentService.GetContentItemAsync(contentName, TimeSpan.FromMinutes(RefreshIntervalMinutes)))?.ToString();
            if (string.IsNullOrEmpty(configString))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<T>(configString);
        }
    }
}
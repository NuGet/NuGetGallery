﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
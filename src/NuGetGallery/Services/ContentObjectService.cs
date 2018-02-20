// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class ContentObjectService : IContentObjectService
    {
        private const int RefreshIntervalHours = 1;

        private readonly IContentService _contentService;

        public ContentObjectService(IContentService contentService)
        {
            _contentService = contentService;

            LoginDiscontinuationConfiguration = 
                new LoginDiscontinuationConfiguration(
                    Enumerable.Empty<string>(), Enumerable.Empty<string>(), Enumerable.Empty<string>());
        }

        public ILoginDiscontinuationConfiguration LoginDiscontinuationConfiguration { get; set; }

        public async Task Refresh()
        {
            LoginDiscontinuationConfiguration = 
                await Refresh<LoginDiscontinuationConfiguration>(Constants.ContentNames.LoginDiscontinuationConfiguration) ??
                new LoginDiscontinuationConfiguration();
        }

        private async Task<T> Refresh<T>(string contentName) 
            where T : class
        {
            var configString = (await _contentService.GetContentItemAsync(contentName, TimeSpan.FromHours(RefreshIntervalHours)))?.ToString();
            if (string.IsNullOrEmpty(configString))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<T>(configString);
        }
    }
}
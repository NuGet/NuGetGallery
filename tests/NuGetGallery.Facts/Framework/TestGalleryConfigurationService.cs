// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGetGallery.Configuration;
using NuGetGallery.Configuration.SecretReader;
using System.Threading.Tasks;
using Moq;

namespace NuGetGallery.Framework
{
    public class TestGalleryConfigurationService : ConfigurationService
    {
        public IDictionary<string, string> Settings = new Dictionary<string, string>();

        public TestGalleryConfigurationService(IAppConfiguration appConfig) : base(new EmptySecretReaderFactory())
        {
            _appConfig = appConfig;
            _featuresConfig = null;
        }

        protected override string GetAppSetting(string settingName)
        {
            if (Settings.ContainsKey(settingName))
            {
                return Settings[settingName];
            }

            // Will cause ResolveConfigObject to populate a class with default values.
            return string.Empty;
        }

        public override async Task<IAppConfiguration> GetCurrent()
        {
            return await Task.FromResult(_appConfig);
        }

        public override async Task<FeatureConfiguration> GetFeatures()
        {
            return await Task.FromResult(_featuresConfig);
        }
    }
}
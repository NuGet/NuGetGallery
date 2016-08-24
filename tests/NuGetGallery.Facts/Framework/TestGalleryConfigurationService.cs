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

        private IAppConfiguration _currentConfig;

        public TestGalleryConfigurationService(IAppConfiguration currentConfig) : base(new EmptySecretReaderFactory())
        {
            _currentConfig = currentConfig;
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
            return await Task.FromResult(_currentConfig);
        }
    }
}
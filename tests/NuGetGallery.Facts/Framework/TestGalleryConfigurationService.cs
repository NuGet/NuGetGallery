// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.KeyVault;
using NuGetGallery.Configuration;
using NuGetGallery.Configuration.SecretReader;

namespace NuGetGallery.Framework
{
    public class TestGalleryConfigurationService : ConfigurationService
    {
        public IDictionary<string, string> Settings = new Dictionary<string, string>();

        public TestGalleryConfigurationService()
        {
            var secretReaderFactory = new EmptySecretReaderFactory();
            SecretInjector = secretReaderFactory.CreateSecretInjector(secretReaderFactory.CreateSecretReader()) as ICachingSecretInjector;
        }

        protected override string GetAppSetting(string settingName)
        {
            if (Settings.TryGetValue(settingName, out var setting))
            {
                return setting;
            }

            // Will cause ResolveConfigObject to populate a class with default values.
            return string.Empty;
        }
    }
}
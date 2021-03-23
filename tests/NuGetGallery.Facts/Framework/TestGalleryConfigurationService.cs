// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGetGallery.Configuration;
using NuGetGallery.Configuration.SecretReader;

namespace NuGetGallery.Framework
{
    public class TestGalleryConfigurationService : ConfigurationService
    {
        public IDictionary<string, string> Settings = new Dictionary<string, string>();
        private Lazy<IAppConfiguration> _baseAppConfiguration;
        /// <summary>
        /// <see cref="ConfigurationService.Current"/> does not return the same object every time
        /// anymore, but a lot of tests depend on old behavior. Making it behave the old way explicitly.
        /// </summary>
        public override IAppConfiguration Current => _baseAppConfiguration.Value;

        public TestGalleryConfigurationService()
        {
            var secretReaderFactory = new EmptySecretReaderFactory();
            SecretInjector = secretReaderFactory.CreateSecretInjector(secretReaderFactory.CreateSecretReader());

            _baseAppConfiguration = new Lazy<IAppConfiguration>(() => base.Current);
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
    }
}
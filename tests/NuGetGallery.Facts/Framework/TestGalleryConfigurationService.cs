// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGetGallery.Configuration;
using NuGetGallery.Configuration.SecretReader;

namespace NuGetGallery.Framework
{
    public class TestGalleryConfigurationService : ConfigurationService
    {
        public IDictionary<string, string> Settings = new Dictionary<string, string>();

        public TestGalleryConfigurationService() : base(new EmptySecretReaderFactory())
        {
        }

        protected override string ReadSetting(string settingName)
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
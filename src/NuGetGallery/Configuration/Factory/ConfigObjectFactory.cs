// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery.Configuration.Factory
{
    public class ConfigObjectFactory<T>
    {
        private ConfigObjectDelegate<T> _delegate;

        public ConfigObjectFactory(ConfigObjectDelegate<T> configDelegate)
        {
            _delegate = configDelegate;
        }

        public T Create(IGalleryConfigurationService configService)
        {
            return _delegate.Get(configService);
        }

        public Task<T> CreateAsync(IGalleryConfigurationService configService)
        {
            return _delegate.GetAsync(configService);
        }
    }
}
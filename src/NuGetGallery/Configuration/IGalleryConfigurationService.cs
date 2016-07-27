// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery.Configuration
{
    public interface IGalleryConfigurationService
    {
        IAppConfiguration Current { get; }

        FeatureConfiguration Features { get; }

        /// <summary>
        /// Gets the site root using the specified protocol
        /// </summary>
        /// <param name="useHttps">If true, the root will be returned in HTTPS form, otherwise, HTTP.</param>
        string GetSiteRoot(bool useHttps);

        /// <summary>
        /// Populate the properties of <param name="instance"></param> from configuration. 
        /// </summary>
        /// <typeparam name="T">The type to populate.</typeparam>
        /// <param name="instance">The instance.</param>
        /// <param name="prefix">The prefix of the properties in the config.</param>
        Task<T> ResolveConfigObject<T>(T instance, string prefix);
    }
}
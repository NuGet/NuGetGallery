// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGetGallery.Configuration
{
    public interface IGalleryConfigurationService
    {
        /// <summary>
        /// Asynchronously access current configuration.
        /// </summary>
        /// <returns>The current configuration.</returns>
        Task<IAppConfiguration> GetCurrent();

        /// <summary>
        /// Asynchronously access features configuration.
        /// </summary>
        /// <returns>The features configuration.</returns>
        Task<FeatureConfiguration> GetFeatures();

        /// <summary>
        /// Synchronously access current configuration in contexts that cannot be async.
        /// Use GetCurrent if possible because this method exclusively uses cached values.
        /// Avoid accessing configuration that changes with this method (e.g. Azure connection strings).
        /// </summary>
        /// <returns>The cached current configuration.</returns>
        IAppConfiguration Current { get; }

        /// <summary>
        /// Synchronously access features configuration in contexts that cannot be async.
        /// Use GetFeatures if possible because this method exclusively uses cached values.
        /// Avoid accessing configuration that changes with this method (e.g. Azure connection strings).
        /// </summary>
        /// <returns>The cached features configuration.</returns>
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

        /// <summary>
        /// Reads a setting by name.
        /// </summary>
        /// <param name="settingName">The name of the desired setting.</param>
        /// <returns>The value of the setting with the specified name.</returns>
        Task<string> ReadSetting(string settingName);
    }
}
// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Configuration
{
    /// <summary>
    /// Asynchronously provides configuration.
    /// Used when all possible configuration cannot be cached, for example RoleEnvironment.
    /// </summary>
    public interface IConfigurationProvider
    {
        /// <summary>
        /// Gets configuration using a <param name="key">key</param> from the service.
        /// </summary>
        /// <typeparam name="T">Converts the configuration from a string into this type.</typeparam>
        /// <param name="key">The key mapping to the desired argument.</param>
        /// <returns>The configuration specified by the key converted to <typeparam name="T">type T</typeparam>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when <param name="key">the key</param> is not mapped to any configuration.</exception>
        /// <exception cref="ConfigurationNullOrEmptyException">Thrown when the configuration mapped to by <param name="key">the key</param> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <param name="key">the key</param> is null or empty.</exception>
        /// <exception cref="NotSupportedException">Thrown when the configuration mapped to by <param name="key">the key</param> cannot be converted into an object of <typeparam name="T">type T</typeparam>.</exception>
        Task<T> GetOrThrowAsync<T>(string key);

        /// <summary>
        /// Gets configuration using a <param name="key">key</param> from the service.
        /// </summary>
        /// <typeparam name="T">Converts the configuration from a string into this type.</typeparam>
        /// <param name="key">The key mapping to the desired argument.</param>
        /// <param name="defaultValue">The value returned if the configuration cannot be found or is malformed.</param>
        /// <returns>The configuration specified by the key converted to <typeparam name="T">type T</typeparam> or <param name="defaultValue">defaultValue</param> if the configuration cannot be found or is malformed.</returns>
        Task<T> GetOrDefaultAsync<T>(string key, T defaultValue = default(T));
    }
}
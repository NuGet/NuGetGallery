// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Configuration
{
    /// <summary>
    /// Abstract base class for ConfigurationProvider that handles conversion and exception handling when provided a Get method.
    /// </summary>
    public abstract class ConfigurationProvider : IConfigurationProvider
    {
        /// <summary>
        /// Gets a value from a given key.
        /// </summary>
        /// <param name="key">The key associated with the desired value.</param>
        /// <returns>The value associated with the given key.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the list of keys.</exception>
        /// <exception cref="ConfigurationNullOrEmptyException">Thrown when the value associated with the given key is null or empty.</exception>
        protected abstract Task<string> Get(string key);

        public async Task<T> GetOrThrowAsync<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException($"{nameof(key)} cannot be null or empty!", nameof(key));
            }

            var value = await Get(key);

            if (string.IsNullOrEmpty(value))
            {
                throw new ConfigurationNullOrEmptyException(key);
            }

            return ConfigurationUtility.ConvertFromString<T>(value);
        }

        public async Task<T> GetOrDefaultAsync<T>(string key, T defaultValue = default(T))
        {
            try
            {
                return await GetOrThrowAsync<T>(key);
            }
            catch (ConfigurationNullOrEmptyException)
            {
                // The value for the specified key is null or empty.
            }
            catch (ArgumentException)
            {
                // The specified key is null or empty.
            }
            catch (KeyNotFoundException)
            {
                // The specified key was not found.
            }
            catch (NotSupportedException)
            {
                // Could not convert an object of type string into an object of type T.
            }
            return defaultValue;
        }
    }
}

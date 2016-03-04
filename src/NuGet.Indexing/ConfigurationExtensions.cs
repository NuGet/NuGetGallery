// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Indexing
{
    public static class ConfigurationExtensions
    {
        /// <summary>Tries to get a value from the configuration service.</summary>
        /// <param name="configuration">The current IConfiguration instance.</param>
        /// <param name="key">The configuration key to fetch the value for.</param>
        /// <param name="defaultValue">The value that will be used if the key is not found.</param>
        /// <returns>The boolean value.</returns>
        public static bool Get(this IConfiguration configuration, string key, bool defaultValue)
        {
            string temp;
            if (configuration.TryGet(key, out temp))
            {
                return string.IsNullOrEmpty(temp)
                    ? defaultValue
                    : string.Equals(temp, "true", StringComparison.OrdinalIgnoreCase);
            }

            return defaultValue;
        }
    }
}
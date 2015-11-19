// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Indexing
{
    public interface IConfiguration
    {
        /// <summary>Gets a value from the configuration service.</summary>
        /// <param name="key">The configuration key to fetch the value for.</param>
        /// <returns>The value or null if the key does not exist.</returns>
        string Get(string key);

        /// <summary>Tries to get a value from the configuration service.</summary>
        /// <param name="key">The configuration key to fetch the value for.</param>
        /// <param name="value">The value that will be set to null if the key is not found.</param>
        /// <returns>True if the value is found. False, otherwise.</returns>
        bool TryGet(string key, out string value);
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Configuration
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Gets the value associated with a key or null if there is no value associated with key.
        /// </summary>
        /// <param name="dictionary">Dictionary to get value from.</param>
        /// <param name="key">The key associated with the desired value.</param>
        /// <returns>The value associated with key in dictionary or null.</returns>
        public static string GetOrNull(this IDictionary<string, string> dictionary, string key)
        {
            string value;
            dictionary.TryGetValue(key, out value);
            return value;
        }

        /// <summary>
        /// Gets the value associated with a key and converts it into T or null if there is no value associated with key or the value could not be converted into T.
        /// </summary>
        /// <typeparam name="T">Type to convert value into.</typeparam>
        /// <param name="dictionary">Dictionary to get value from.</param>
        /// <param name="key">The key associated with the desired value.</param>
        /// <returns>The value associated with key converted into T or null.</returns>
        public static T? GetOrNull<T>(this IDictionary<string, string> dictionary, string key) where T : struct
        {
            string valueString;
            if (!dictionary.TryGetValue(key, out valueString))
            {
                return null;
            }

            try
            {
                return ConfigurationUtility.ConvertFromString<T>(valueString);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the value associated with a key and converts it into T or throws.
        /// </summary>
        /// <typeparam name="T">Type to convert value into.</typeparam>
        /// <param name="dictionary">Dictionary to get value from.</param>
        /// <param name="key">The key associated with the desired value.</param>
        /// <returns>The value associated with key converted into T.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when there is no value associated with key in the dictionary.</exception>
        /// <exception cref="NotSupportedException">Thrown when a conversion from string to T is impossible.</exception>
        public static T GetOrThrow<T>(this IDictionary<string, string> dictionary, string key) where T : struct
        {
            return ConfigurationUtility.ConvertFromString<T>(dictionary[key]);
        }
    }
}

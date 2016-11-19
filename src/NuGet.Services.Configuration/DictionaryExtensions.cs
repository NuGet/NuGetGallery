// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Configuration
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Gets the value associated with a key and converts it into T or returns a specified default.
        /// </summary>
        /// <typeparam name="T">Type to convert value into.</typeparam>
        /// <param name="dictionary">Dictionary to get value from.</param>
        /// <param name="key">The key associated with the desired value.</param>
        /// <param name="defaultValue">The default value to return if the key cannot be found.</param>
        /// <returns>The value associated with key converted into T or null.</returns>
        /// <exception cref="NotSupportedException">Thrown when a conversion from string to T is impossible.</exception>
        public static T GetOrDefault<T>(this IDictionary<string, string> dictionary, string key, T defaultValue = default(T))
        {
            string valueString;
            return dictionary.TryGetValue(key, out valueString) ? ConfigurationUtility.ConvertFromString<T>(valueString) : defaultValue;
        }

        /// <summary>
        /// Gets the value associated with a key and converts it into T or throws.
        /// </summary>
        /// <typeparam name="T">Type to convert value into.</typeparam>
        /// <param name="dictionary">Dictionary to get value from.</param>
        /// <param name="key">The key associated with the desired value.</param>
        /// <returns>The value associated with the key converted into T.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the key cannot be found in the dictionary.</exception>
        /// <exception cref="NotSupportedException">Thrown when a conversion from string to T is impossible.</exception>
        /// <exception cref="ArgumentException">Thrown when the value associated with the key in the dictionary is null or empty.</exception>
        public static T GetOrThrow<T>(this IDictionary<string, string> dictionary, string key)
        {
            var value = dictionary[key];

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"Value associated with key {key} in the dictionary is null or empty.");
            }

            return ConfigurationUtility.ConvertFromString<T>(value);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;

namespace NuGet.Services.Configuration
{
    /// <summary>
    /// Thrown when the configuration value associated with a key is null or empty.
    /// </summary>
    [Serializable]
    public class ConfigurationNullOrEmptyException : Exception
    {
        public string Key { get; }

        public ConfigurationNullOrEmptyException()
        {
        }

        public ConfigurationNullOrEmptyException(string key)
            : base(GetMessageFromKey(key))
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException($"{nameof(key)} cannot be null or empty!", nameof(key));
            }

            Key = key;
        }

        private static string GetMessageFromKey(string key)
        {
            return $"The configuration value associated with key {key} is null or empty.";
        }
    }
}

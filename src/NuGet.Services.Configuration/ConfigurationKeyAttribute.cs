// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;

namespace NuGet.Services.Configuration
{
    /// <summary>
    /// Used to specify a configuration key in a <see cref="Configuration"/> subclass while using a <see cref="ConfigurationFactory"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigurationKeyAttribute : Attribute
    {
        public ConfigurationKeyAttribute(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException($"{nameof(key)} cannot be null or empty!", nameof(key));
            }

            Key = key;
        }

        /// <summary>
        /// The configuration key to pass into the <see cref="IConfigurationProvider"/> used by the <see cref="ConfigurationFactory"/>.
        /// </summary>
        public string Key { get; }
    }
}

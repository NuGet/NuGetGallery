// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;

namespace NuGet.Services.Configuration
{
    /// <summary>
    /// Used to specify a prefix to add to the configuration key in a <see cref="Configuration"/> subclass while using a <see cref="ConfigurationFactory"/>.
    /// Can be applied to both individual properties and entire classes.
    /// If applied to an individual property, it will specify the prefix of that property alone.
    /// If applied to an entire class, it will specify the prefix for all properties except those with their own <see cref="ConfigurationKeyPrefixAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    public class ConfigurationKeyPrefixAttribute : Attribute
    {
        public ConfigurationKeyPrefixAttribute(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentException($"{nameof(prefix)} cannot be null or empty!", nameof(prefix));
            }

            Prefix = prefix;
        }

        /// <summary>
        /// The prefix that will be added to the start of the configuration key passed into the <see cref="IConfigurationProvider"/> used by the <see cref="ConfigurationFactory"/>.
        /// </summary>
        public string Prefix { get; }
    }
}

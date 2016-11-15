// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System.Threading.Tasks;

namespace NuGet.Services.Configuration
{
    /// <summary>
    /// Injects a <see cref="Configuration"/> subclass with configuration.
    /// </summary>
    public interface IConfigurationFactory
    {
        /// <summary>
        /// Injects a <see cref="Configuration"/> subclass with configuration.
        /// Enumerates through all of the properties of <typeparam name="T">T</typeparam> and injects configuration using the name of each property.
        /// </summary>
        /// <typeparam name="T">
        /// Subclass of configuration to inject with configuration.
        /// Configuration will only be injected into properties of the class.
        /// </typeparam>
        /// <returns>A <typeparam name="T">T</typeparam> with all of its properties injected with configuration.</returns>
        Task<T> Get<T>() where T : Configuration, new();
    }
}

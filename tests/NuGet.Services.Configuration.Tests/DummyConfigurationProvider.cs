// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Configuration.Tests
{
    /// <summary>
    /// Returns "test value" for all configuration keys unless the given key is null or empty.
    /// </summary>
    public class DummyConfigurationProvider : ConfigurationProvider
    {
        protected override Task<string> Get(string key)
        {
            return Task.FromResult("test value");
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests.Support
{
    public class CommonFixture : IAsyncLifetime
    {
        public TestSettings TestSettings { get; private set; }

        public async Task InitializeAsync()
        {
            TestSettings = await TestSettings.CreateAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests
{
    public sealed class AspireFunctionalTestFixture : IAsyncLifetime
    {
        internal const string CloudTestWorkerEnvironmentVariable = "CloudTestWorkerCustomVstestExe";

        public Task InitializeAsync()
        {
            if (Environment.GetEnvironmentVariable(CloudTestWorkerEnvironmentVariable) == null)
            {
                throw new InvalidOperationException(
                    $"{CloudTestWorkerEnvironmentVariable} must be present when the Aspire test harness is excluded.");
            }

            _ = new GalleryTestFixture();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}

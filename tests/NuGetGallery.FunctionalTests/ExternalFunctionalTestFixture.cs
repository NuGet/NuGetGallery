// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests
{
    public sealed class AspireFunctionalTestFixture : IAsyncLifetime
    {
        public Task InitializeAsync()
        {
            _ = new GalleryTestFixture();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}

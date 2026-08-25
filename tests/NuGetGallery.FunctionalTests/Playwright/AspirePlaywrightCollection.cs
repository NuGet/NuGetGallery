// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright
{
    [CollectionDefinition(Definition)]
    public sealed class AspirePlaywrightCollection : ICollectionFixture<AspirePlaywrightFixture>
    {
        public const string Definition = "Aspire Playwright test collection";
    }
}

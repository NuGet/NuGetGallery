// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.FunctionalTests
{
    [CollectionDefinition(GalleryTestCollection.Definition)]
    public class GalleryTestCollection
        : ICollectionFixture<GalleryTestFixture>
        , IClassFixture<ClearMachineCacheFixture>
    {
        public const string Definition = "Gallery test collection";

        // This class its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
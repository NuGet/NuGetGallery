// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Base class for all the gallery test classes.
    /// Has the common functions which individual test classes would use.
    /// </summary>
    [Collection(GalleryTestCollection.Definition)]
    public abstract class GalleryTestBase
    {
        protected GalleryTestBase(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
        }

        public ITestOutputHelper TestOutputHelper { get; private set; }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery
{
    public class PackageStatusFacts
    {
        public void AssertAvailableNotChanged()
        {
            Assert.Equal(0, (int)PackageStatus.Available);
        }

        public void AssertDeletedNotChanged()
        {
            Assert.Equal(1, (int)PackageStatus.Deleted);
        }
    }
}

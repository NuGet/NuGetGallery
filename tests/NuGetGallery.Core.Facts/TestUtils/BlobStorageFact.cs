﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery
{
    public class BlobStorageFact : FactAttribute
    {
        public BlobStorageFact()
        {
            Skip = BlobStorageFixture.SkipReason;
        }
    }
}

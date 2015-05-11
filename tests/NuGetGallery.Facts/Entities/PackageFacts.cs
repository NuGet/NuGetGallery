// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Xunit;

namespace NuGetGallery.Entities
{
    public class PackageFacts
    {
        public class TheDefaultConstructor
        {
            [Fact]
            public void WillDefaultListedToTrue()
            {
                var p = new Package();
                Assert.True(p.Listed);
            }
        }
    }
}
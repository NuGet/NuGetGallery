﻿using Xunit;

namespace NuGetGallery.Data
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
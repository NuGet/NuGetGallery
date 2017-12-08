// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Auditing
{
    public class ObfuscatorFacts
    {
        [Theory]
        [InlineData("fe80:ffff:1111:023c:1ff:fe23:4567:890a", "fe80:ffff:1111:23c::")]
        [InlineData("192.168.0.100", "192.168.0.0")]
        [InlineData("400.400.400.400", "400.400.400.400")]
        [InlineData(null, null)]
        [InlineData("hello", "hello")]
        public void ObfuscateIpFact(string input, string expectedOutput)
        {
            // Arrange & Act
            var output = Obfuscator.ObfuscateIp(input);

            // Assert
            Assert.Equal(expectedOutput, output);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Auditing
{
    public class PIIUtilFacts
    {
        [Theory]
        [InlineData("1.2.3.4", "1.2.3.0")]
        [InlineData("1234", "1234")]
        [InlineData(null, null)]
        [InlineData(".1234", ".1234")]
        public void ObfuscateIpFact(string input, string expectedOutput)
        {
            // Arrange & Act
            var output = PIIUtil.ObfuscateIp(input);

            // Assert
            Assert.Equal(expectedOutput, output);
        }
    }
}

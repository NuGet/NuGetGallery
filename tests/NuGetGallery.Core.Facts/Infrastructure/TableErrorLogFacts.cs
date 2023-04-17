// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Elmah;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class TableErrorLogFacts
    {
        public class TheObfuscateMethod
        {
            [Fact]
            public void HandlesMissingForwardedHeader()
            {
                // Arrange
                var error = new Error();

                // Act
                TableErrorLog.Obfuscate(error);

                // Assert
                Assert.DoesNotContain("HTTP_X_FORWARDED_FOR", error.ServerVariables.Keys.Cast<string>());
            }

            [Theory]
            [InlineData("", "")]
            [InlineData(",", ",")]
            [InlineData(" ", " ")]
            [InlineData("127.0.0.1", "127.0.0.0")]
            [InlineData("127.1.2.3,127.1.2.4", "127.1.2.0,127.1.2.0")]
            [InlineData("127.1.2.3   ,  127.1.2.4", "127.1.2.0,127.1.2.0")]
            public void ObfuscatesForwardedHeader(string input, string expected)
            {
                // Arrange
                var error = new Error();
                error.ServerVariables["HTTP_X_FORWARDED_FOR"] = input;

                // Act
                TableErrorLog.Obfuscate(error);

                // Assert
                Assert.Equal(expected, error.ServerVariables["HTTP_X_FORWARDED_FOR"]);
            }
        }
    }
}

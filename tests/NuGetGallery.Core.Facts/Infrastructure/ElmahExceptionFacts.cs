// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class ElmahExceptionFacts
    {
        [Fact]
        public void ValidElmahException()
        {
            // Arrange & Act
            var exception = new Exception("Boo", new Exception("Inner Boo"));
            Dictionary<string, string> serverVariables = new Dictionary<string, string>();
            serverVariables.Add("AUTH_USER", "booUser");
            var elmahException = new ElmahException(exception, serverVariables);

            // Assert
            Assert.Equal("booUser", elmahException.ServerVariables["AUTH_USER"]);
            Assert.Equal("Inner Boo", elmahException.InnerException.Message);
            Assert.Equal("Boo", elmahException.Message);
            Assert.Equal(elmahException.Message, exception.Message);
            Assert.Equal(elmahException.ToString(), exception.ToString());
            Assert.Equal(elmahException.InnerException.ToString(), exception.InnerException.ToString());
            Assert.Equal(elmahException.GetBaseException(), exception.GetBaseException());
            Assert.Equal(elmahException.HelpLink, exception.HelpLink);
        }

        [Fact]
        public void NullElmahExceptionServerVariables()
        {
            // Arrange & Act
            var exception = new Exception("Boo", new Exception("Inner Boo"));
            var elmahException = new ElmahException(exception, null);

            // Assert
            Assert.Empty(elmahException.ServerVariables.Keys);
        }
    }
}

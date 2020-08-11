// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class BatchProcessingExceptionTests
    {
        [Fact]
        public void Constructor_WhenExceptionIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new BatchProcessingException(inner: null));

            Assert.Equal("inner", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentIsValid_ReturnsInstance()
        {
            var innerException = new Exception();
            var exception = new BatchProcessingException(innerException);

            Assert.Equal("A failure occurred while processing a catalog batch.", exception.Message);
            Assert.Same(innerException, exception.InnerException);
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class StringInternerTests
    {
        [Fact]
        public void DifferentInstancesBecomeTheSame()
        {
            // Arrange
            var aBefore = new StringBuilder("a").Append("b").ToString();
            var bBefore = new StringBuilder("a").Append("b").ToString();
            var interner = new StringInterner();

            // Act
            var aAfter = interner.Intern(aBefore);
            var bAfter = interner.Intern(aBefore);

            // Assert
            Assert.NotSame(aBefore, bBefore);
            Assert.Same(aAfter, bAfter);
        }

        [Fact]
        public void SameInstancesStayTheSame()
        {
            // Arrange
            var aBefore = "a";
            var bBefore = aBefore;
            var interner = new StringInterner();

            // Act
            var aAfter = interner.Intern(aBefore);
            var bAfter = interner.Intern(aBefore);

            // Assert
            Assert.Same(aBefore, bBefore);
            Assert.Same(aAfter, bAfter);
        }
    }
}

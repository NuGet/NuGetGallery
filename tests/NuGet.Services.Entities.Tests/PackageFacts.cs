// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;
using Xunit;

namespace NuGet.Services.Entities.Tests
{
    public class PackageFacts
    {
        [Fact]
        public void HasReadMe_InternalColumnMapped()
        {
            // Arrange & Act
            var attributes = typeof(Package).GetProperty("HasReadMeInternal").GetCustomAttributes(typeof(ColumnAttribute), true) as ColumnAttribute[];

            // Assert
            Assert.Single(attributes);
            Assert.Equal("HasReadMe", attributes[0].Name);
        }

        [Fact]
        public void HasReadMe_WhenInternalIsNullReturnsFalse()
        {
            // Arrange & Act
            var package = new Package { HasReadMeInternal = null };

            // Assert
            Assert.False(package.HasReadMe);
        }

        [Fact]
        public void HasReadMe_WhenInternalIsTrueReturnsTrue()
        {
            // Arrange & Act
            var package = new Package { HasReadMeInternal = true };

            // Assert
            Assert.True(package.HasReadMe);
        }

        [Fact]
        public void HasReadMe_WhenSetToFalseSavesInternalAsNull()
        {
            // Arrange & Act
            var package = new Package { HasReadMeInternal = true };
            package.HasReadMe = false;

            // Assert
            Assert.Null(package.HasReadMeInternal);
        }

        [Fact]
        public void HasReadMe_WhenSetToTrueSavesInternalAsTrue()
        {
            // Arrange & Act
            var package = new Package { HasReadMeInternal = null };
            package.HasReadMe = true;

            // Assert
            Assert.True(package.HasReadMeInternal);
        }
    }
}

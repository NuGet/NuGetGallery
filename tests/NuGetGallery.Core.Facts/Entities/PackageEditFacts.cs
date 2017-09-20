// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;
using Xunit;

namespace NuGetGallery
{
    public class PackageEditFacts
    {
        [Fact]
        public void ReadMeState_InternalColumnMapped()
        {
            // Arrange & Act
            var attributes = typeof(PackageEdit).GetProperty("ReadMeStateInternal").GetCustomAttributes(typeof(ColumnAttribute), true) as ColumnAttribute[];

            // Assert
            Assert.Equal(1, attributes.Length);
            Assert.Equal("ReadMeState", attributes[0].Name);
        }

        [Theory]
        [InlineData("changed", PackageEditReadMeState.Changed)]
        [InlineData("deleted", PackageEditReadMeState.Deleted)]
        [InlineData(null, PackageEditReadMeState.Unchanged)]
        [InlineData("unchanged", PackageEditReadMeState.Unchanged)]
        public void ReadMeState_WhenInternalIsStringReturnsState(string internalValue, PackageEditReadMeState expectedState)
        {
            // Arrange & Act
            var edit = new PackageEdit { ReadMeStateInternal = internalValue };

            // Assert
            Assert.Equal(expectedState, edit.ReadMeState);
        }

        [Fact]
        public void ReadMeState_WhenSetToUnchangedSavesInternalAsNull()
        {
            // Arrange & Act
            var edit = new PackageEdit { ReadMeStateInternal = "changed" };
            edit.ReadMeState = PackageEditReadMeState.Unchanged;

            // Assert
            Assert.Null(edit.ReadMeStateInternal);
        }

        [Theory]
        [InlineData(PackageEditReadMeState.Changed, "changed")]
        [InlineData(PackageEditReadMeState.Deleted, "deleted")]
        public void ReadMeState_WhenSetToChangedSavesInternalAsString(PackageEditReadMeState changedState, string expectedInternalValue)
        {
            // Arrange & Act
            var edit = new PackageEdit { ReadMeStateInternal = null };
            edit.ReadMeState = changedState;

            // Assert
            Assert.Equal(expectedInternalValue, edit.ReadMeStateInternal);
        }
    }
}

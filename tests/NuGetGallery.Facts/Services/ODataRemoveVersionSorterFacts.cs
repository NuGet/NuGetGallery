// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Linq;
using Xunit;

namespace NuGetGallery
{
    public class ODataRemoveVersionSorterFacts
    {
        [Fact]
        public void RemoveVersionSortRemovesThenByOnVersion()
        {
            // Arrange
            var packageAb = new V2FeedPackage { Id = "A", Version = "B" };
            var packageAa = new V2FeedPackage { Id = "A", Version = "A" };
            var packageCa = new V2FeedPackage { Id = "C", Version = "A" };

            var source = new[] { packageAb, packageCa, packageAa }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Id).ThenBy(p => p.Version);
            var resultB = source.WithoutVersionSort().OrderBy(p => p.Id).ThenBy(p => p.Version);

            // Assert
            Assert.Equal(new[] { packageAa, packageAb, packageCa }, resultA);
            Assert.Equal(new[] { packageAb, packageAa, packageCa }, resultB);
        }

        [Fact]
        public void RemoveVersionSortRemovesThenByDescendingOnVersion()
        {
            // Arrange
            var packageAb = new V2FeedPackage { Id = "A", Version = "B" };
            var packageAa = new V2FeedPackage { Id = "A", Version = "A" };
            var packageAc = new V2FeedPackage { Id = "A", Version = "C" };

            var source = new[] { packageAb, packageAc, packageAa }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Id).ThenByDescending(p => p.Version);
            var resultB = source.WithoutVersionSort().OrderBy(p => p.Id).ThenByDescending(p => p.Version);

            // Assert
            Assert.Equal(new[] { packageAc, packageAb, packageAa }, resultA);
            Assert.Equal(new[] { packageAb, packageAc, packageAa }, resultB);
        }

        [Fact]
        public void RemoveVersionSortRemovesThenByWhenItIsNestedInsideAnotherThenBy()
        {
            // Arrange
            var packageAb = new V2FeedPackage { Id = "A", Version = "B" };
            var packageAa = new V2FeedPackage { Id = "A", Version = "A" };
            var packageAc = new V2FeedPackage { Id = "A", Version = "C" };

            var source = new[] { packageAb, packageAc, packageAa }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.Version);
            var resultB = source.WithoutVersionSort().OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.Version);

            // Assert
            Assert.Equal(new[] { packageAc, packageAb, packageAa }, resultA);
            Assert.Equal(new[] { packageAb, packageAc, packageAa }, resultB);
        }

        [Fact]
        public void RemoveVersionSortRemovesThenByWhenVersionIsRepresentedInAWrapperObject()
        {
            // Arrange
            var packageAb = new { Id = "A", WrapperObject = new { Version = "B" } };
            var packageAa = new { Id = "A", WrapperObject = new { Version = "A" } };
            var packageAc = new { Id = "A", WrapperObject = new { Version = "C" } };

            var source = new[] { packageAb, packageAc, packageAa }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.WrapperObject.Version);
            var resultB = source.WithoutVersionSort().OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.WrapperObject.Version);

            // Assert
            Assert.Equal(new[] { packageAc, packageAb, packageAa }, resultA);
            Assert.Equal(new[] { packageAb, packageAc, packageAa }, resultB);
        }
    }
}
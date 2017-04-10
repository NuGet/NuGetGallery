// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Linq;
using NuGetGallery.OData;
using Xunit;

namespace NuGetGallery
{
    public class ODataRemoveSorterFacts
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
            var resultB = source.WithoutSortOnColumn("Version").OrderBy(p => p.Id).ThenBy(p => p.Version);

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
            var resultB = source.WithoutSortOnColumn("Version").OrderBy(p => p.Id).ThenByDescending(p => p.Version);

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
            var resultB = source.WithoutSortOnColumn("Version").OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.Version);

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
            var resultB = source.WithoutSortOnColumn("Version").OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.WrapperObject.Version);

            // Assert
            Assert.Equal(new[] { packageAc, packageAb, packageAa }, resultA);
            Assert.Equal(new[] { packageAb, packageAc, packageAa }, resultB);
        }

        [Fact]
        public void RemoveIdSortRemovesThenByOnId()
        {
            // Arrange
            var package1 = new V2FeedPackage { Id = "A", Version = "C" };
            var package2 = new V2FeedPackage { Id = "B", Version = "A" };
            var package3 = new V2FeedPackage { Id = "A", Version = "A" };

            var source = new[] { package1, package2, package3 }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Version).ThenBy(p => p.Id);
            var resultB = source.WithoutSortOnColumn("Id").OrderBy(p => p.Version).ThenBy(p => p.Id);

            // Assert
            Assert.Equal(new[] { package3, package2, package1 }, resultA);
            Assert.Equal(new[] { package2, package3, package1 }, resultB);
        }

        [Fact]
        public void RemoveIdSortRemovesThenByDescendingOnId()
        {
            // Arrange
            var package1 = new V2FeedPackage { Id = "A", Version = "A" };
            var package2 = new V2FeedPackage { Id = "B", Version = "A" };
            var package3 = new V2FeedPackage { Id = "C", Version = "A" };

            var source = new[] { package1, package2, package3 }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Version).ThenByDescending(p => p.Id);
            var resultB = source.WithoutSortOnColumn("Id").OrderBy(p => p.Version).ThenByDescending(p => p.Id);

            // Assert
            Assert.Equal(new[] { package3, package2, package1 }, resultA);
            Assert.Equal(new[] { package1, package2, package3 }, resultB);
        }

        [Fact]
        public void RemoveIdSortRemovesThenByWhenItIsNestedInsideAnotherThenBy()
        {
            // Arrange
            var package1 = new V2FeedPackage { Id = "B", Version = "A" };
            var package2 = new V2FeedPackage { Id = "A", Version = "A" };
            var package3 = new V2FeedPackage { Id = "C", Version = "A" };

            var source = new[] { package1, package2, package3 }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Version).ThenBy(p => p.Version).ThenByDescending(p => p.Id);
            var resultB = source.WithoutSortOnColumn("Id").OrderBy(p => p.Version).ThenBy(p => p.Version).ThenByDescending(p => p.Id);

            // Assert
            Assert.Equal(new[] { package3, package1, package2 }, resultA);
            Assert.Equal(new[] { package1, package2, package3 }, resultB);
        }

        [Fact]
        public void RemoveIdSortRemovesThenByWhenVersionIsRepresentedInAWrapperObject()
        {
            // Arrange
            var package1 = new { Version = "A", WrapperObject = new { Id = "B" } };
            var package2 = new { Version = "A", WrapperObject = new { Id = "A" } };
            var package3 = new { Version = "A", WrapperObject = new { Id = "C" } };

            var source = new[] { package1, package2, package3 }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Version).ThenBy(p => p.Version).ThenByDescending(p => p.WrapperObject.Id);
            var resultB = source.WithoutSortOnColumn("Id").OrderBy(p => p.Version).ThenBy(p => p.Version).ThenByDescending(p => p.WrapperObject.Id);

            // Assert
            Assert.Equal(new[] { package3, package1, package2 }, resultA);
            Assert.Equal(new[] { package1, package2, package3 }, resultB);
        }

        [Fact]
        public void RemoveSortWithInvalidColumn()
        {
            // Arrange
            var package1 = new V2FeedPackage { Id = "B", Version = "A" };
            var package2 = new V2FeedPackage { Id = "A", Version = "A" };
            var package3 = new V2FeedPackage { Id = "C", Version = "A" };

            var source = new[] { package1, package2, package3 }.AsQueryable();

            // Act
            var result = source.WithoutSortOnColumn("Dummy").OrderBy(p => p.Version).ThenBy(p => p.Id);

            // Assert
            Assert.Equal(new[] { package2, package1, package3 }, result);
        }
    }
}
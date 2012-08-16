using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace NuGetGallery
{
    public class ODataRemoveVersionSorterFacts
    {
        [Fact]
        public void RemoveVersionSortRemovesThenByOnVersion()
        {
            // Arrange
            var package_AB = new V2FeedPackage { Id = "A", Version = "B"};
            var package_AA = new V2FeedPackage { Id = "A", Version = "A"};
            var package_CA = new V2FeedPackage { Id = "C", Version = "A"};

            var source = new[] { package_AB, package_CA, package_AA }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Id).ThenBy(p => p.Version);
            var resultB = source.WithoutVersionSort().OrderBy(p => p.Id).ThenBy(p => p.Version);

            // Assert
            Assert.Equal(new[] { package_AA, package_AB, package_CA }, resultA);
            Assert.Equal(new[] { package_AB, package_AA, package_CA }, resultB);
        }

        [Fact]
        public void RemoveVersionSortRemovesThenByDescendingOnVersion()
        {
            // Arrange
            var package_AB = new V2FeedPackage { Id = "A", Version = "B" };
            var package_AA = new V2FeedPackage { Id = "A", Version = "A" };
            var package_AC = new V2FeedPackage { Id = "A", Version = "C" };

            var source = new[] { package_AB, package_AC, package_AA }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Id).ThenByDescending(p => p.Version);
            var resultB = source.WithoutVersionSort().OrderBy(p => p.Id).ThenByDescending(p => p.Version);

            // Assert
            Assert.Equal(new[] { package_AC, package_AB, package_AA }, resultA);
            Assert.Equal(new[] { package_AB, package_AC, package_AA }, resultB);
        }

        [Fact]
        public void RemoveVersionSortRemovesThenByWhenItIsNestedInsideAnotherThenBy()
        {
            // Arrange
            var package_AB = new V2FeedPackage { Id = "A", Version = "B" };
            var package_AA = new V2FeedPackage { Id = "A", Version = "A" };
            var package_AC = new V2FeedPackage { Id = "A", Version = "C" };

            var source = new[] { package_AB, package_AC, package_AA }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.Version);
            var resultB = source.WithoutVersionSort().OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.Version);

            // Assert
            Assert.Equal(new[] { package_AC, package_AB, package_AA }, resultA);
            Assert.Equal(new[] { package_AB, package_AC, package_AA }, resultB);
        }

        [Fact]
        public void RemoveVersionSortRemovesThenByWhenVersionIsRepresentedInAWrapperObject()
        {
            // Arrange
            var package_AB = new { Id = "A", WrapperObject = new { Version = "B" } };
            var package_AA = new { Id = "A", WrapperObject = new { Version = "A" } };
            var package_AC = new { Id = "A", WrapperObject = new { Version = "C" } };

            var source = new[] { package_AB, package_AC, package_AA }.AsQueryable();

            // Act
            var resultA = source.OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.WrapperObject.Version);
            var resultB = source.WithoutVersionSort().OrderBy(p => p.Id).ThenBy(p => p.Id).ThenByDescending(p => p.WrapperObject.Version);

            // Assert
            Assert.Equal(new[] { package_AC, package_AB, package_AA }, resultA);
            Assert.Equal(new[] { package_AB, package_AC, package_AA }, resultB);
        }
    }
}

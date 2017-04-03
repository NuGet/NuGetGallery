// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using VDS.RDF;
using Xunit;

namespace CatalogTests.Helpers
{
    public class NuGetVersionUtilityTests
    {
        [Theory]
        [InlineData("1.0.0-alpha", "1.0.0-alpha")]
        [InlineData("1.0.0-alpha.1", "1.0.0-alpha.1")]
        [InlineData("1.0.0-alpha+githash", "1.0.0-alpha")]
        [InlineData("1.0.0.0", "1.0.0")]
        [InlineData("invalid", "invalid")]
        public void NormalizeVersion(string input, string expected)
        {
            // Arrange & Act
            var actual = NuGetVersionUtility.NormalizeVersion(input);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("[1.0.0-alpha, )", "[1.0.0-alpha, )")]
        [InlineData("1.0.0-alpha.1", "[1.0.0-alpha.1, )")]
        [InlineData("[1.0.0-alpha+githash, )", "[1.0.0-alpha, )")]
        [InlineData("[1.0, 2.0]", "[1.0.0, 2.0.0]")]
        [InlineData("invalid", "invalid")]
        public void NormalizeVersionRange(string input, string expected)
        {
            // Arrange & Act
            var actual = NuGetVersionUtility.NormalizeVersionRange(input);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("1.0.0-alpha.1", "1.0.0-alpha.1")]
        [InlineData("1.0.0-alpha+githash", "1.0.0-alpha+githash")]
        [InlineData("1.0.0.0", "1.0.0")]
        [InlineData("invalid", "invalid")]
        public void GetFullVersionString(string input, string expected)
        {
            // Arrange & Act
            var actual = NuGetVersionUtility.GetFullVersionString(input);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("1.0.0-alpha.1", true)]
        [InlineData("1.0.0-alpha.1+githash", true)]
        [InlineData("1.0.0-alpha+githash", true)]
        [InlineData("1.0.0+githash", true)]
        [InlineData("1.0.0-alpha", false)]
        [InlineData("1.0.0", false)]
        [InlineData("invalid", false)]
        public void IsVersionSemVer2(string input, bool expected)
        {
            // Arrange & Act
            var actual = NuGetVersionUtility.IsVersionSemVer2(input);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("[1.0.0-alpha.1, 2.0.0)", true)]
        [InlineData("[1.0.0+githash, 2.0.0)", true)]
        [InlineData("[1.0.0-alpha, 2.0.0-alpha.1)", true)]
        [InlineData("[1.0.0, 2.0.0+githash)", true)]
        [InlineData("[1.0.0-alpha, 2.0.0)", false)]
        [InlineData("invalid", false)]
        public void IsVersionRangeSemVer2(string input, bool expected)
        {
            // Arrange & Act
            var actual = NuGetVersionUtility.IsVersionRangeSemVer2(input);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void IsGraphSemVer2_WithSemVer2PackageVersion_IsSemVer2()
        {
            // Arrange
            var graph = new Graph();

            // Act
            var actual = NuGetVersionUtility.IsGraphSemVer2(TD.SemVer2Version, TD.ResourceUri, graph);

            // Assert
            Assert.True(actual);
        }

        [Fact]
        public void IsGraphSemVer2_WithSemVer1PackageVersionAndNoDependencies_IsSemVer1()
        {
            // Arrange
            var graph = new Graph();

            // Act
            var actual = NuGetVersionUtility.IsGraphSemVer2(TD.SemVer1Version, TD.ResourceUri, graph);

            // Assert
            Assert.False(actual);
        }

        [Fact]
        public void IsGraphSemVer2_WithSemVer1PackageVersionAndSemVer2VerbatimVersion_IsSemVer2()
        {
            // Arrange
            var graph = new Graph();
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.ResourceUri)),
                graph.CreateUriNode(Schema.Predicates.VerbatimVersion),
                graph.CreateLiteralNode(TD.SemVer2Version));

            // Act
            var actual = NuGetVersionUtility.IsGraphSemVer2(TD.SemVer1Version, TD.ResourceUri, graph);

            // Assert
            Assert.True(actual);
        }

        [Fact]
        public void IsGraphSemVer2_WithSemVer1PackageVersionAndSemVer1Dependencies_IsSemVer1()
        {
            // Arrange
            var graph = new Graph();
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.ResourceUri)),
                graph.CreateUriNode(Schema.Predicates.DependencyGroup),
                graph.CreateUriNode(new Uri(TD.DependencyGroupUri)));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyGroupUri)),
                graph.CreateUriNode(Schema.Predicates.Type),
                graph.CreateUriNode(Schema.DataTypes.PackageDependencyGroup));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyGroupUri)),
                graph.CreateUriNode(Schema.Predicates.Dependency),
                graph.CreateUriNode(new Uri(TD.DependencyUriA)));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyUriA)),
                graph.CreateUriNode(Schema.Predicates.Type),
                graph.CreateUriNode(Schema.DataTypes.PackageDependency));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyUriA)),
                graph.CreateUriNode(Schema.Predicates.Range),
                graph.CreateLiteralNode(TD.SemVer1Version));

            // Act
            var actual = NuGetVersionUtility.IsGraphSemVer2(TD.SemVer1Version, TD.ResourceUri, graph);

            // Assert
            Assert.False(actual);
        }

        [Fact]
        public void IsGraphSemVer2_WithSemVer1PackageVersionAndSemVer2Dependencies_IsSemVer2()
        {
            // Arrange
            var graph = new Graph();
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.ResourceUri)),
                graph.CreateUriNode(Schema.Predicates.DependencyGroup),
                graph.CreateUriNode(new Uri(TD.DependencyGroupUri)));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyGroupUri)),
                graph.CreateUriNode(Schema.Predicates.Type),
                graph.CreateUriNode(Schema.DataTypes.PackageDependencyGroup));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyGroupUri)),
                graph.CreateUriNode(Schema.Predicates.Dependency),
                graph.CreateUriNode(new Uri(TD.DependencyUriA)));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyUriA)),
                graph.CreateUriNode(Schema.Predicates.Type),
                graph.CreateUriNode(Schema.DataTypes.PackageDependency));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyUriA)),
                graph.CreateUriNode(Schema.Predicates.Range),
                graph.CreateLiteralNode(TD.SemVer1Version));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyGroupUri)),
                graph.CreateUriNode(Schema.Predicates.Dependency),
                graph.CreateUriNode(new Uri(TD.DependencyUriB)));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyUriB)),
                graph.CreateUriNode(Schema.Predicates.Type),
                graph.CreateUriNode(Schema.DataTypes.PackageDependency));
            graph.Assert(
                graph.CreateUriNode(new Uri(TD.DependencyUriB)),
                graph.CreateUriNode(Schema.Predicates.Range),
                graph.CreateLiteralNode(TD.SemVer2Version));

            // Act
            var actual = NuGetVersionUtility.IsGraphSemVer2(TD.SemVer1Version, TD.ResourceUri, graph);

            // Assert
            Assert.True(actual);
        }

        /// <summary>
        /// Test data.
        /// </summary>
        private static class TD
        {
            public const string SemVer1Version = "1.0.0-alpha";
            public const string SemVer2Version = "1.0.0-alpha.1";
            public const string ResourceUri = "https://example.com/packageid.1.0.0-a.1.json";
            public const string DependencyGroupUri = ResourceUri + "#dependencyGroup/net45";
            public const string DependencyUriA = ResourceUri + "#dependencyGroup/net45/newtonsoft.json";
            public const string DependencyUriB = ResourceUri + "#dependencyGroup/net45/nuget.versioning";
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Registration;
using VDS.RDF;
using Xunit;

namespace CatalogTests.Registration
{
    public class RegistrationCatalogEntryTests
    {
        private const string ResourceUri = "http://example.com/packageid.1.0.0-a.1.json";
        private const string Id = "PackageId";
        private const string Version = "1.0.0-a.1";
        private const bool IsExistingItem = true;

        [Fact]
        public void Promote_WhenShouldBeIncluded_ReturnsCompleteKeyValuePair()
        {
            // Arrange
            var graph = InitializeGraph(ResourceUri, Id, Version);
            var isDelegateInvoked = new DelegateInvoked(shouldInclude: true);

            // Act
            var pair = RegistrationCatalogEntry.Promote(
                ResourceUri,
                graph,
                isDelegateInvoked.Delegate,
                IsExistingItem);

            // Assert
            Assert.Equal(pair.Key.RegistrationKey.Id, Id);
            Assert.Equal(pair.Key.Version, Version);
            Assert.NotNull(pair.Value);
            Assert.Equal(pair.Value.IsExistingItem, IsExistingItem);
            Assert.Same(pair.Value.Graph, graph);
            Assert.Equal(pair.Value.ResourceUri, ResourceUri);
            Assert.True(isDelegateInvoked.Invoked, "The delegate to determine whether an entry should have been invoked.");
        }

        [Fact]
        public void Promote_WhenShouldNotBeIncluded_ReturnsCompleteKeyValuePairWithNullValue()
        {
            // Arrange
            var graph = InitializeGraph(ResourceUri, Id, Version);
            var isDelegateInvoked = new DelegateInvoked(shouldInclude: false);

            // Act
            var pair = RegistrationCatalogEntry.Promote(
                ResourceUri,
                graph,
                isDelegateInvoked.Delegate,
                IsExistingItem);

            // Assert
            Assert.Equal(pair.Key.RegistrationKey.Id, Id);
            Assert.Equal(pair.Key.Version, Version);
            Assert.Null(pair.Value);
            Assert.True(isDelegateInvoked.Invoked, "The delegate to determine whether an entry should have been invoked.");
        }

        [Fact]
        public void Promote_WithDelete_ReturnsCompleteKeyValuePairWithNullValue()
        {
            // Arrange
            var graph = InitializeGraph(ResourceUri, Id, Version);
            graph.Assert(
                graph.CreateUriNode(new Uri(ResourceUri)),
                graph.CreateUriNode(Schema.Predicates.Type),
                graph.CreateUriNode(Schema.DataTypes.PackageDelete));
            var delegateInvoked = new DelegateInvoked(shouldInclude: true);

            // Act
            var pair = RegistrationCatalogEntry.Promote(
                ResourceUri,
                graph,
                delegateInvoked.Delegate,
                IsExistingItem);

            // Assert
            Assert.Equal(pair.Key.RegistrationKey.Id, Id);
            Assert.Equal(pair.Key.Version, Version);
            Assert.Null(pair.Value);
            Assert.False(delegateInvoked.Invoked, "The delegate to determine whether an entry should not have been invoked.");
        }

        private static Graph InitializeGraph(string resourceUri, string id, string version)
        {
            var graph = new Graph();
            graph.Assert(
                graph.CreateUriNode(new Uri(resourceUri)),
                graph.CreateUriNode(Schema.Predicates.Id),
                graph.CreateLiteralNode(id));
            graph.Assert(
                graph.CreateUriNode(new Uri(resourceUri)),
                graph.CreateUriNode(Schema.Predicates.Version),
                graph.CreateLiteralNode(version));
            return graph;
        }

        private class DelegateInvoked
        {
            public DelegateInvoked(bool shouldInclude)
            {
                Invoked = false;
                Delegate = (k, u, g) =>
                {
                    Invoked = true;
                    return shouldInclude;
                };
            }

            public bool Invoked { get; private set; }
            public ShouldIncludeRegistrationPackage Delegate { get; }
        }
    }
}

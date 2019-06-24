// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Metadata.Catalog;
using VDS.RDF;
using Xunit;

namespace CatalogTests
{
    public class ReadOnlyGraphTests
    {
        private const string PackageId = "Newtonsoft.Json";
        private const string PackageVersion = "9.0.1";
        private const string ReadOnlyMessage = "This RDF graph cannot be modified.";
        private static readonly Uri PackageUri = new Uri("http://example/newtonsoft.json/9.0.1.json");

        [Fact]
        public void MutationsOfTheOriginalGraphDoNotEffectTheReadOnlyGraph()
        {
            // Arrange
            var mutableGraph = GetIdMutableGraph();
            var readOnlyGraph = new ReadOnlyGraph(mutableGraph);

            // Act
            mutableGraph.Assert(
                mutableGraph.CreateUriNode(PackageUri),
                mutableGraph.CreateUriNode(Schema.Predicates.Version),
                mutableGraph.CreateLiteralNode(PackageVersion));

            // Assert
            var versionTriples = readOnlyGraph.GetTriplesWithSubjectPredicate(
                readOnlyGraph.CreateUriNode(PackageUri),
                readOnlyGraph.CreateUriNode(Schema.Predicates.Version));
            Assert.Empty(versionTriples);

            VerifyIdTriple(readOnlyGraph);
            Assert.Equal(1, readOnlyGraph.Triples.Count);
        }

        [Fact]
        public void RejectsAssertOfSubjectPredicateObject()
        {
            // Arrange
            var readOnlyGraph = new ReadOnlyGraph(GetIdMutableGraph());

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => readOnlyGraph.Assert(
                readOnlyGraph.CreateUriNode(PackageUri),
                readOnlyGraph.CreateUriNode(Schema.Predicates.Version),
                readOnlyGraph.CreateLiteralNode(PackageId)));
            Assert.Equal(ReadOnlyMessage, exception.Message);

            VerifyIdTriple(readOnlyGraph);
            Assert.Equal(1, readOnlyGraph.Triples.Count);
        }

        [Fact]
        public void RejectsAssertOfSingleTriple()
        {
            // Arrange
            var readOnlyGraph = new ReadOnlyGraph(GetIdMutableGraph());

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => readOnlyGraph.Assert(new Triple(
                readOnlyGraph.CreateUriNode(PackageUri),
                readOnlyGraph.CreateUriNode(Schema.Predicates.Version),
                readOnlyGraph.CreateLiteralNode(PackageId))));
            Assert.Equal(ReadOnlyMessage, exception.Message);

            VerifyIdTriple(readOnlyGraph);
            Assert.Equal(1, readOnlyGraph.Triples.Count);
        }

        [Fact]
        public void RejectsAssertOfTripleEnumerable()
        {
            // Arrange
            var readOnlyGraph = new ReadOnlyGraph(GetIdMutableGraph());

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => readOnlyGraph.Assert(new[]
            {
                new Triple(
                    readOnlyGraph.CreateUriNode(PackageUri),
                    readOnlyGraph.CreateUriNode(Schema.Predicates.Version),
                    readOnlyGraph.CreateLiteralNode(PackageVersion))
            }));
            Assert.Equal(ReadOnlyMessage, exception.Message);

            VerifyIdTriple(readOnlyGraph);
            Assert.Equal(1, readOnlyGraph.Triples.Count);
        }

        [Fact]
        public void RejectsMergeWhenKeepOriginalGraphUriIsNotSpecified()
        {
            // Arrange
            var readOnlyGraph = new ReadOnlyGraph(GetIdMutableGraph());
            var otherGraph = GetVersionMutableGraph();

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => readOnlyGraph.Merge(otherGraph));
            Assert.Equal(ReadOnlyMessage, exception.Message);

            VerifyIdTriple(readOnlyGraph);
            Assert.Equal(1, readOnlyGraph.Triples.Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RejectsMergeWhenKeepOriginalGraphUriIsSpecified(bool keepOriginalGraphUri)
        {
            // Arrange
            var readOnlyGraph = new ReadOnlyGraph(GetIdMutableGraph());
            var otherGraph = GetVersionMutableGraph();

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => readOnlyGraph.Merge(otherGraph, keepOriginalGraphUri));
            Assert.Equal(ReadOnlyMessage, exception.Message);

            VerifyIdTriple(readOnlyGraph);
            Assert.Equal(1, readOnlyGraph.Triples.Count);
        }

        [Fact]
        public void RejectsRetractOfSubjectPredicateObject()
        {
            // Arrange
            var readOnlyGraph = new ReadOnlyGraph(GetIdMutableGraph());

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => readOnlyGraph.Retract(
                readOnlyGraph.CreateUriNode(PackageUri),
                readOnlyGraph.CreateUriNode(Schema.Predicates.Id),
                readOnlyGraph.CreateLiteralNode(PackageId)));
            Assert.Equal(ReadOnlyMessage, exception.Message);

            VerifyIdTriple(readOnlyGraph);
            Assert.Equal(1, readOnlyGraph.Triples.Count);
        }

        [Fact]
        public void RejectsRetractOfSingleTriple()
        {
            // Arrange
            var readOnlyGraph = new ReadOnlyGraph(GetIdMutableGraph());

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => readOnlyGraph.Retract(new Triple(
                readOnlyGraph.CreateUriNode(PackageUri),
                readOnlyGraph.CreateUriNode(Schema.Predicates.Id),
                readOnlyGraph.CreateLiteralNode(PackageId))));
            Assert.Equal(ReadOnlyMessage, exception.Message);

            VerifyIdTriple(readOnlyGraph);
            Assert.Equal(1, readOnlyGraph.Triples.Count);
        }

        [Fact]
        public void RejectsRetractOfTripleEnumerable()
        {
            // Arrange
            var readOnlyGraph = new ReadOnlyGraph(GetIdMutableGraph());

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => readOnlyGraph.Retract(new[]
            {
                new Triple(
                    readOnlyGraph.CreateUriNode(PackageUri),
                    readOnlyGraph.CreateUriNode(Schema.Predicates.Id),
                    readOnlyGraph.CreateLiteralNode(PackageId))
            }));
            Assert.Equal(ReadOnlyMessage, exception.Message);

            VerifyIdTriple(readOnlyGraph);
            Assert.Equal(1, readOnlyGraph.Triples.Count);
        }

        private static Graph GetIdMutableGraph()
        {
            var mutableGraph = new Graph();
            mutableGraph.Assert(
                mutableGraph.CreateUriNode(PackageUri),
                mutableGraph.CreateUriNode(Schema.Predicates.Id),
                mutableGraph.CreateLiteralNode(PackageId));
            return mutableGraph;
        }

        private static Graph GetVersionMutableGraph()
        {
            var mutableGraph = new Graph();
            mutableGraph.Assert(
                mutableGraph.CreateUriNode(PackageUri),
                mutableGraph.CreateUriNode(Schema.Predicates.Version),
                mutableGraph.CreateLiteralNode(PackageVersion));
            return mutableGraph;
        }

        private static void VerifyIdTriple(ReadOnlyGraph readOnlyGraph)
        {
            var idTriples = readOnlyGraph.GetTriplesWithSubjectPredicate(
                            readOnlyGraph.CreateUriNode(PackageUri),
                            readOnlyGraph.CreateUriNode(Schema.Predicates.Id));
            Assert.Single(idTriples);
            Assert.Equal(PackageId, ((LiteralNode)idTriples.First().Object).Value);
        }
    }
}

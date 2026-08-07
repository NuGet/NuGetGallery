// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StagedSymbolTerminalStateProcessorFacts
    {
        [Theory]
        [InlineData(StagingArtifactStatus.Ready)]
        [InlineData(StagingArtifactStatus.ValidationFailed)]
        public async Task UpdatesOnlyMatchingHashPair(StagingArtifactStatus status)
        {
            var facts = new Facts();

            await facts.Target.ProcessAsync(facts.ValidationSet, facts.SymbolPackage, status);

            Assert.Equal(status, facts.Artifact.Status);
            Assert.Equal(status == StagingArtifactStatus.Ready, facts.Artifact.ValidatedDate.HasValue);
            facts.Context.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Theory]
        [InlineData("tracking")]
        [InlineData("symbol")]
        [InlineData("parent")]
        public async Task IgnoresStaleOutcome(string staleValue)
        {
            var facts = new Facts();
            if (staleValue == "tracking")
            {
                facts.ValidationSet.ValidationTrackingId = Guid.NewGuid();
            }
            else if (staleValue == "symbol")
            {
                facts.SymbolPackage.Hash = "replacement-symbol-hash";
            }
            else
            {
                facts.ParentPackage.Hash = "replacement-parent-hash";
            }

            await facts.Target.ProcessAsync(
                facts.ValidationSet,
                facts.SymbolPackage,
                StagingArtifactStatus.Ready);

            Assert.Equal(StagingArtifactStatus.Validating, facts.Artifact.Status);
            Assert.Null(facts.Artifact.ValidatedDate);
            facts.Context.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        private sealed class Facts
        {
            public Facts()
            {
                var trackingId = Guid.NewGuid();
                ParentPackage = new Package { Hash = "parent-hash" };
                SymbolPackage = new SymbolPackage
                {
                    Key = 43,
                    Hash = "symbol-hash",
                };
                Artifact = new StagedSymbolArtifact
                {
                    SymbolPackageKey = SymbolPackage.Key,
                    SymbolPackage = SymbolPackage,
                    StagingEntry = new StagingEntry { Package = ParentPackage },
                    ValidationTrackingId = trackingId,
                    ContentHash = SymbolPackage.Hash,
                    ParentContentHash = ParentPackage.Hash,
                    Status = StagingArtifactStatus.Validating,
                };
                ValidationSet = new PackageValidationSet
                {
                    ValidationTrackingId = trackingId,
                };

                Context = new Mock<IEntitiesContext>();
                Context
                    .SetupGet(x => x.StagedSymbolArtifacts)
                    .Returns(CreateDbSet(Artifact).Object);
                Context.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

                Target = new StagedSymbolTerminalStateProcessor(
                    Context.Object,
                    Mock.Of<ILogger<StagedSymbolTerminalStateProcessor>>());
            }

            public Package ParentPackage { get; }
            public SymbolPackage SymbolPackage { get; }
            public StagedSymbolArtifact Artifact { get; }
            public PackageValidationSet ValidationSet { get; }
            public Mock<IEntitiesContext> Context { get; }
            public StagedSymbolTerminalStateProcessor Target { get; }
        }

        private static Mock<DbSet<T>> CreateDbSet<T>(params T[] values) where T : class
        {
            var query = ((IEnumerable<T>)values).AsQueryable();
            var dbSet = new Mock<DbSet<T>>();
            dbSet.As<IQueryable<T>>().Setup(x => x.Provider).Returns(query.Provider);
            dbSet.As<IQueryable<T>>().Setup(x => x.Expression).Returns(query.Expression);
            dbSet.As<IQueryable<T>>().Setup(x => x.ElementType).Returns(query.ElementType);
            dbSet.As<IQueryable<T>>().Setup(x => x.GetEnumerator()).Returns(() => query.GetEnumerator());
            return dbSet;
        }
    }
}

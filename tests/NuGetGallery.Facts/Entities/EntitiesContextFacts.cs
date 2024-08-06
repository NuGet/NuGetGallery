// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Entities
{
    public class EntitiesContextFacts
    {
        private const string ConnectionString = "Data Source=(localdb)\\mssqllocaldb; Initial Catalog=NuGetGallery; Integrated Security=True; MultipleActiveResultSets=True";

        [Fact]
        public void SaveChangesFailsInReadOnlyMode()
        {
            var ec = new EntitiesContext(ConnectionString, readOnly: true);
            Assert.Throws<ReadOnlyModeException>(() => ec.SaveChanges());
        }

        [Fact]
        public async Task SaveChangesAsyncFailsInReadOnlyMode()
        {
            var ec = new EntitiesContext(ConnectionString, readOnly: true);
            await Assert.ThrowsAsync<ReadOnlyModeException>(() => ec.SaveChangesAsync());
        }

        [Fact]
        public async Task SaveChangesAsyncWithCancellationTokenFailsInReadOnlyMode()
        {
            var ec = new EntitiesContext(ConnectionString, readOnly: true);
            await Assert.ThrowsAsync<ReadOnlyModeException>(() => ec.SaveChangesAsync(CancellationToken.None));
        }

        [Fact]
        public void WithQueryHintDisposeClearsQueryHint()
        {
            var entitiesContext = new EntitiesContext(ConnectionString, readOnly: true);

            Assert.Null(entitiesContext.QueryHint);
            var disposable = entitiesContext.WithQueryHint("RECOMPILE");
            Assert.Equal("RECOMPILE", entitiesContext.QueryHint);
            disposable.Dispose();
            Assert.Null(entitiesContext.QueryHint);
        }

        [Fact]
        public void MultipleQueryHintsAreNotSupported()
        {
            var entitiesContext = new EntitiesContext(ConnectionString, readOnly: true);
            entitiesContext.WithQueryHint("RECOMPILE");

            Assert.Throws<InvalidOperationException>(() => entitiesContext.WithQueryHint("RECOMPILE"));

        }
    }
}

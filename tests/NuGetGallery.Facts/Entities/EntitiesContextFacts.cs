// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Entities
{
    class EntitiesContextFacts
    {
        [Fact]
        public void SaveChangesFailsInReadOnlyMode()
        {
            var ec = new EntitiesContext("", readOnly: true);
            Assert.Throws<ReadOnlyException>(() => ec.Users.Add(new User
            {
                Key = -1,
                Username = "FredFredFred",
            }));
        }

        [Fact]
        public void WithQueryHintDisposeClearsQueryHint()
        {
            var entitiesContext = new EntitiesContext("", readOnly: true);

            Assert.Null(entitiesContext.QueryHint);
            var disposable = entitiesContext.WithQueryHint("RECOMPILE");
            Assert.Equal("RECOMPILE", entitiesContext.QueryHint);
            disposable.Dispose();
            Assert.Null(entitiesContext.QueryHint);
        }

        [Fact]
        public void MultipleQueryHintsAreNotSupported()
        {
            var entitiesContext = new EntitiesContext("", readOnly: true);
            entitiesContext.WithQueryHint("RECOMPILE");

            Assert.Throws<InvalidOperationException>(() => entitiesContext.WithQueryHint("RECOMPILE"));

        }
    }
}

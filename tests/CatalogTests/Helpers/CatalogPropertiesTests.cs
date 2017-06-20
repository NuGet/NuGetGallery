// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Helpers;
using Xunit;

namespace CatalogTests.Helpers
{
    public class CatalogPropertiesTests
    {
        [Fact]
        public void Constructor_InitializesPropertiesWithNull()
        {
            var properties = new CatalogProperties(lastCreated: null, lastDeleted: null, lastEdited: null);

            Assert.Null(properties.LastCreated);
            Assert.Null(properties.LastDeleted);
            Assert.Null(properties.LastEdited);
        }

        [Fact]
        public void Constructor_InitializesPropertiesWithNonNullValues()
        {
            var lastCreated = DateTime.Now;
            var lastDeleted = lastCreated.AddMinutes(1);
            var lastEdited = lastDeleted.AddMinutes(1);
            var properties = new CatalogProperties(lastCreated, lastDeleted, lastEdited);

            Assert.Equal(lastCreated, properties.LastCreated);
            Assert.Equal(lastDeleted, properties.LastDeleted);
            Assert.Equal(lastEdited, properties.LastEdited);
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data;
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
    }
}

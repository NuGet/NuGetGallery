// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Xunit;

namespace NuGet.Protocol.Catalog
{
    public class CatalogLeafItemFacts
    {
        [Fact]
        public void JsonSerializationException()
        {
            var leafItem = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/registration3-gz-semver2/newtonsoft.json/12.0.1.json",
                Type = CatalogLeafType.PackageDetails,
                CommitId = "47065e84-b83a-434f-9619-1b2f17df91b9",
                CommitTimestamp = DateTimeOffset.Parse("2019-10-10T00:00:00.00+00:00"),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "12.0.1"
            };

            var result = JsonConvert.SerializeObject(leafItem);

            Assert.Equal(TestData.CatalogLeafItem, result);
        }
    }
}

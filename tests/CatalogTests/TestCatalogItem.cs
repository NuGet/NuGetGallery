// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;

namespace CatalogTests
{
    class TestCatalogItem : CatalogItem
    {
        string _name;
        Uri _type;

        public TestCatalogItem(string name)
        {
            _name = name;
            _type = new Uri("http://test.org/schema#TestItem");
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            string id = BaseAddress + _name + ".json";

            JObject obj = new JObject
                {
                    { "name", _name },
                    { "@id", id },
                    { "@type", _type },
                    { "@context", new JObject { { "@vocab", "http://test.org/schema#" } } }
                };

            return new StringStorageContent(obj.ToString(), "application/json");
        }

        public override Uri GetItemType()
        {
            return _type;
        }

        public override Uri GetItemAddress()
        {
            return new Uri(BaseAddress, _name);
        }
    }
}

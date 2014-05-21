using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
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

        public override string CreateContent(CatalogContext context)
        {
            string id = GetBaseAddress() + _name + ".json";

            JObject obj = new JObject
                {
                    { "name", _name },
                    { "@id", id },
                    { "@type", _type },
                    { "@context", new JObject { { "@vocab", "http://test.org/schema#" } } }
                };

            return obj.ToString();
        }

        public override Uri GetItemType()
        {
            return _type;
        }

        protected override string GetItemIdentity()
        {
            return _name;
        }
    }
}

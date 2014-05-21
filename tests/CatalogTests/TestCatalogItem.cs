using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;

namespace CatalogTests
{
    class TestCatalogItem : CatalogItem
    {
        string _name;
        string _type;

        public TestCatalogItem(string name)
        {
            _name = name;
            _type = "http://test.org/schema#TestItem";
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

        public override string GetItemType()
        {
            return _type;
        }

        protected override string GetItemName()
        {
            return _name;
        }
    }
}

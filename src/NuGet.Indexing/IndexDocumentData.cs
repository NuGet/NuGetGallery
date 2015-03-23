using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Documents;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using NuGetGallery;

namespace NuGet.Indexing
{
    public class IndexDocumentData
    {
        public Package Package { get; set; }
        public int Checksum {get; set; }
        public IEnumerable<string> Feeds { get; set; }

        public static IndexDocumentData FromDocument(Document doc)
        {
            return new IndexDocumentData()
            {
                Package = PackageJson.FromJson(JObject.Parse(doc.GetField("Data").StringValue)),
                Checksum = Int32.Parse(doc.GetFieldable("Checksum").StringValue),
                Feeds = doc.GetFields("CuratedFeed").Select(f => f.StringValue).ToList()
            };
        }
    }
}

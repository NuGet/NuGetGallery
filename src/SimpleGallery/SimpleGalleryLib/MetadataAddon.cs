using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace SimpleGalleryLib
{
    public class MetadataAddon : GraphAddon
    {
        private JObject _json = null;

        public MetadataAddon(Stream nupkgStream)
        {
            Init(nupkgStream);
        }

        private void Init(Stream nupkgStream)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                nupkgStream.Seek(0, SeekOrigin.Begin);
                nupkgStream.CopyTo(stream);
                nupkgStream.Seek(0, SeekOrigin.Begin);

                ZipArchive zip = new ZipArchive(stream);
                var manifest = zip.Entries.Where(e => e.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (manifest != null)
                {
                    using (StreamReader reader = new StreamReader(manifest.Open()))
                    {
                        _json = JObject.Parse(reader.ReadToEnd());
                    }
                }
            }
        }

        public const string EMASchema = "http://schema.azure.com/ema#";

        public override void ApplyToGraph(IGraph graph, IUriNode parent)
        {
            if (_json != null)
            {
                var jObject = _json.DeepClone() as JObject;

                jObject.Add("@id", parent.Uri.AbsoluteUri);

                var context = JObject.Parse("{ \"@vocab\": \"http://schema.azure.com/ema#\" }");
                jObject.Add("@context", context);

                string jsonString = jObject.ToString();
                IGraph metadataGraph = NuGet.Services.Metadata.Catalog.Utils.CreateGraph(jsonString);

                foreach (var triple in metadataGraph.Triples)
                {
                    graph.Assert(triple);
                }
            }
        }
    }
}

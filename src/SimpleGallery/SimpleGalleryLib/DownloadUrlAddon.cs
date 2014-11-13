using NuGet.Packaging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace SimpleGalleryLib
{
    public class DownloadUrlAddon : GraphAddon
    {
        private Uri _downloadUrl;
        private static readonly Uri SchemaDownloadUrl = new Uri("http://schema.nuget.org/schema#DownloadUrl");

        public DownloadUrlAddon(Uri downloadUrl)
        {
            _downloadUrl = downloadUrl;
        }


        public override void ApplyToGraph(IGraph graph, IUriNode parent)
        {
            graph.Assert(parent, graph.CreateUriNode(SchemaDownloadUrl), graph.CreateLiteralNode(_downloadUrl.AbsoluteUri));
        }
    }
}

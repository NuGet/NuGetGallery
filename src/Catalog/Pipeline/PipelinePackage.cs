using System;
using System.IO;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PipelinePackage
    {
        public PipelinePackage()
        {
        }

        public PipelinePackage(Stream stream)
            : this(stream, DateTime.UtcNow)
        {
        }

        public PipelinePackage(Stream stream, DateTime published, string owner = null)
        {
            Stream = stream;
            Published = published;
            Owner = owner;
        }

        public Stream Stream { get; set; }
        public DateTime Published { get; set; }
        public string Owner { get; set; }
    }
}

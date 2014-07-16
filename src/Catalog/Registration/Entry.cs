using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class Entry
    {
        public Uri Uri { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
    }
}

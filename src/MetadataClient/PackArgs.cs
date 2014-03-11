using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PowerArgs;

namespace MetadataClient
{
    public class PackArgs
    {
        [ArgDescription("Nuspec file name")]
        public string Nuspec { get; set; }
    }
}

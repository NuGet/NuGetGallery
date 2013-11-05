using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Backend.Worker
{
    public class JobRequest
    {
        public string Name { get; private set; }
        public Dictionary<string, string> Configuration { get; private set; }

        public JobRequest(string name, Dictionary<string, string> configuration)
        {
            Name = name;
            Configuration = configuration;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Backend
{
    public class JobRequest
    {
        public string Name { get; private set; }
        public Dictionary<string, string> Parameters { get; private set; }

        public JobRequest(string name, Dictionary<string, string> parameters)
        {
            Name = name;
            Parameters = parameters;
        }
    }
}

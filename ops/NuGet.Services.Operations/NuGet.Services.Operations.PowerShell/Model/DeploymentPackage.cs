using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class DeploymentPackage
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public Uri Uri { get; set; }
        public string Commit { get; set; }
    }
}

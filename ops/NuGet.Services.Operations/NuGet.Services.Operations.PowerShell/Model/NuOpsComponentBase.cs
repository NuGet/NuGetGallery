using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace NuGet.Services.Operations.Model
{
    public abstract class NuOpsComponentBase
    {
        public string Type { get; set; }
        public string Name { get; set; }
    }
}

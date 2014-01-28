using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class UriResource : Resource
    {
        public static readonly string ElementName = "uri";

        public Uri Uri { get; set; }
    }
}

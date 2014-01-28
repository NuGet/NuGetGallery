using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Serialization
{
    public class UriResourceParser : IParser<Resource>
    {
        public Resource Parse(XElement element)
        {
            return new UriResource()
            {
                Name = element.AttributeValue("name"),
                Type = UriResource.ElementName,
                Uri = element.ValueAs<Uri>(s => new Uri(s))
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Serialization
{
    public class AzureStorageResourceParser : IParser<Resource>
    {
        public Resource Parse(XElement element)
        {
            return new AzureStorageResource()
            {
                Name = element.AttributeValue("name"),
                Type = AzureStorageResource.ElementName,
                AccountName = element.Value
            };
        }
    }
}

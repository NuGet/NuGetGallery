using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Serialization
{
    public class AzureRoleServiceParser : IParser<Service>
    {
        public Service Parse(XElement element)
        {
            return new AzureRoleService()
            {
                Name = element.AttributeValue("name"),
                Type = AzureRoleService.ElementName,
                CloudServiceName = element.Value
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Serialization
{
    public class SharepointSecretStoreParser : IParser<SecretStore>
    {
        public SecretStore Parse(XElement element)
        {
            return new SharepointSecretStore()
            {
                Name = element.AttributeValue("name"),
                Type = SharepointSecretStore.ElementName,
                Version = element.AttributeValueAs<Version>("version", Version.Parse),
                Url = element.ValueAs<Uri>(s => new Uri(s))
            };
        }
    }
}

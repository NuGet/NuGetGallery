using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Serialization
{
    public class TeamCityPackageSourceParser : IParser<PackageSource>
    {
        public PackageSource Parse(XElement element)
        {
            return new TeamCityPackageSource()
            {
                Name = element.AttributeValue("name"),
                Type = TeamCityPackageSource.ElementName,
                BuildType = element.AttributeValue("buildType"),
                ServerUri = element.ValueAs<Uri>(s => new Uri(s)),
                Version = element.AttributeValueAs<Version>("version", Version.Parse)
            };
        }
    }
}

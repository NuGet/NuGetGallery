using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Services.Publish
{
    public class NuSpecJsonPublishImpl : PublishImpl
    {
        static ISet<string> Files = new HashSet<string> { "nuspec.json" };

        public NuSpecJsonPublishImpl(IRegistrationOwnership registrationOwnership)
            : base(registrationOwnership)
        {
        }

        protected override bool IsMetadataFile(string fullName)
        {
            return Files.Contains(fullName);
        }

        protected override JObject CreateMetadataObject(string fullname, Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            JObject obj = JObject.Parse(reader.ReadToEnd());
            return obj;
        }

        protected override Uri GetItemType()
        {
            return Schema.DataTypes.Package;
        }

        protected override IList<string> Validate(Stream packageStream)
        {
            return null;
        }
    }
}
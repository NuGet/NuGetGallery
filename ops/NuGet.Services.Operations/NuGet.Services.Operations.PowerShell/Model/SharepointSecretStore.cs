using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class SharepointSecretStore : SecretStore
    {
        public static readonly string ElementName = "sharepoint";

        public Version Version { get; set; }
        public Uri Url { get; set; }
    }
}

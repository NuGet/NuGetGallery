using Newtonsoft.Json;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHubAdvisoryTransformer.Entities {
    public class Advisory {

        [JsonProperty(PropertyName="url")]
        public Uri Url { get; set; }

        [JsonProperty(PropertyName = "severity")]
        public int Severity { get; set; }

        [JsonProperty(PropertyName = "versions")]
        public string Versions { get; set; }
    }
}

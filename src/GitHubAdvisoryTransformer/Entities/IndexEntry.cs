

using Newtonsoft.Json;

namespace GitHubAdvisoryTransformer.Entities
{
    public class IndexEntry
    {
        [JsonProperty(PropertyName="@name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName="@id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName="@updated")]
        public DateTime Updated { get; set; }

        [JsonProperty(PropertyName="comment")]
        public string Comment { get;set; }
    }
}

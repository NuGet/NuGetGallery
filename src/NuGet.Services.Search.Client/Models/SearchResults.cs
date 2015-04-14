using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace NuGet.Services.Search.Models
{
    public class SearchResults
    {
        public int TotalHits { get; set; }
        public DateTime? IndexTimestamp { get; set; }
        public ICollection<JObject> Data { get; set; }
    }
}

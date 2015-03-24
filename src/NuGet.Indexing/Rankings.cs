using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public abstract class Rankings
    {
        public static readonly string FileName = "rankings.v1.json";

        public abstract string Path { get; }
        protected abstract JObject LoadJson();

        public IDictionary<string, IDictionary<string, int>> Load()
        {
            JObject obj = LoadJson();
            if (obj == null)
            {
                return new Dictionary<string, IDictionary<string, int>>();
            }

            IDictionary<string, IDictionary<string, int>> result = new Dictionary<string, IDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

            foreach (JProperty prop in obj.Properties())
            {
                IDictionary<string, int> ranking = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                int order = 1;
                foreach (JToken item in (JArray)prop.Value)
                {
                    ranking.Add(item.ToString(), order++);
                }

                result.Add(prop.Name, ranking);
            }

            return result;
        }
    }
}

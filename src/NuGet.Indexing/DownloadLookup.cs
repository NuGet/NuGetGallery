using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public abstract class DownloadLookup
    {
        public static readonly string FileName = "downloads.v1.json";
        public abstract string Path { get; }
        protected abstract JObject LoadJson();

        public IDictionary<string, IDictionary<string, int>> Load()
        {
            IDictionary<string, IDictionary<string, int>> result = new Dictionary<string, IDictionary<string, int>>();

            JObject obj = LoadJson();
            if (obj != null)
            {
                foreach (JObject record in obj.PropertyValues())
                {
                    string id = record["Id"].ToString().ToLowerInvariant();
                    string version = record["Version"].ToString();
                    int downloads = record["Downloads"].ToObject<int>();

                    IDictionary<string, int> versions;
                    if (!result.TryGetValue(id, out versions))
                    {
                        versions = new Dictionary<string, int>();
                        result.Add(id, versions);
                    }

                    versions.Add(version, downloads);
                }
            }

            return result;
        }
    }
}
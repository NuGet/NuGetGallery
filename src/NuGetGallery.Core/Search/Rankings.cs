using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public abstract class Rankings
    {
        protected abstract JObject LoadJson();

        public IDictionary<string, IDictionary<string, int>> Load()
        {
            JObject obj = LoadJson();

            IDictionary<string, IDictionary<string, int>> result = new Dictionary<string, IDictionary<string, int>>();

            foreach (JProperty prop in obj.Properties())
            {
                IDictionary<string, int> ranking = new Dictionary<string, int>();

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

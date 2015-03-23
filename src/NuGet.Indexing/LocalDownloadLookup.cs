using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace NuGet.Indexing
{
    public class LocalDownloadLookup : DownloadLookup
    {
        string _path;

        public override string Path { get { return _path; } }

        public LocalDownloadLookup(string path)
        {
            _path = path;
        }

        protected override JObject LoadJson()
        {
            using (StreamReader textReader = new StreamReader(Path))
            {
                using (JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    return JObject.Load(jsonReader);
                }
            }
        }
    }
}
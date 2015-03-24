using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public class LocalDownloadCounts : DownloadCounts
    {
        string _path;

        public override string Path { get { return _path; } }

        public LocalDownloadCounts(string path)
        {
            _path = path;
        }

        protected override JObject LoadJson()
        {
            string json;
            using (TextReader reader = new StreamReader(Path))
            {
                json = reader.ReadToEnd();
            }
            JObject obj = JObject.Parse(json);
            return obj;
        }
    }
}

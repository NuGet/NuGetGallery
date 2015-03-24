using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public abstract class DownloadCounts
    {
        public static readonly string FileName = "downloads.v1.json";
        public abstract string Path { get; }
        protected abstract JObject LoadJson();

        public IDictionary<int, DownloadCountRecord> Load()
        {
            JObject obj = LoadJson();
            if (obj == null)
            {
                return new Dictionary<int, DownloadCountRecord>();
            }
            return obj.ToObject<IDictionary<int, DownloadCountRecord>>();
        }
    }

    public class DownloadCountRecord
    {
        public int Downloads { get; set; }
        public int RegistrationDownloads { get; set; }
        public int Installs { get; set; }
        public int Updates { get; set; }
    }
}

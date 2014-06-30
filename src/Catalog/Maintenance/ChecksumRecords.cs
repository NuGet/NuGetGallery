using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public abstract class ChecksumRecords
    {
        public IDictionary<int, JObject> Data { get; private set; }
        public DateTime TimestampUtc { get; set; }

        protected ChecksumRecords()
        {
            Data = new Dictionary<int, JObject>();
            TimestampUtc = DateTime.MinValue;
        }

        public async Task Load()
        {
            var json = await LoadJson();
            if (json != null)
            {
                TimestampUtc = json.Value<DateTime>("commitTimestamp");
                Data = json.Value<JObject>("data").Properties().ToDictionary(
                    p => Int32.Parse(p.Name),
                    p => p.Value.Value<JObject>());
            }
            else
            {
                TimestampUtc = DateTime.MinValue;
                Data = new Dictionary<int, JObject>();
            }
        }

        public Task Save()
        {
            var document = new JObject(
                new JProperty("commitTimestamp", TimestampUtc.ToString("O")),
                new JProperty("data", new JObject(
                    Data.Select(p =>
                        new JProperty(p.Key.ToString(), p.Value)))));
            return SaveJson(document);
        }

        protected abstract Task<JObject> LoadJson();
        protected abstract Task SaveJson(JObject obj);
    }

    public class LocalFileChecksumRecords : ChecksumRecords
    {
        public string FilePath { get; private set; }

        public LocalFileChecksumRecords(string filePath)
        {
            FilePath = filePath;
        }

        protected override async Task<JObject> LoadJson()
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }
            using (var reader = new StreamReader(FilePath))
            {
                return JObject.Parse(await reader.ReadToEndAsync());
            }
        }

        protected override async Task SaveJson(JObject obj)
        {
            string dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var writer = new StreamWriter(FilePath, append: false))
            {
                await writer.WriteAsync(obj.ToString(Formatting.None));
            }
        }
    }
}

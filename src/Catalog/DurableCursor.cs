using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class DurableCursor : ReadWriteCursor
    {
        Uri _address;
        Storage _storage;

        public DurableCursor(Uri address, Storage storage)
        {
            _address = address;
            _storage = storage;
        }

        public override async Task Save()
        {
            JObject obj = new JObject { { "value", Value.ToString("O") } };
            StorageContent content = new StringStorageContent(obj.ToString(), "application/json", "no-store");
            await _storage.Save(_address, content);
        }

        public override async Task Load()
        {
            string json = await _storage.LoadString(_address);

            if (json == null)
            {
                Value = DateTime.MinValue.ToUniversalTime();
                return;
            }

            JObject obj = JObject.Parse(json);
            Value = obj["value"].ToObject<DateTime>();
        }
    }
}

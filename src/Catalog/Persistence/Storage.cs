using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class Storage
    {
        string _baseAddress;

        public abstract Task Save(Uri resourceUri, StorageContent content);
        public abstract Task<StorageContent> Load(Uri resourceUri);
        public abstract Task Delete(Uri resourceUri);

        public async Task<string> LoadString(Uri resourceUri)
        {
            StorageContent content = await Load(resourceUri);
            if (content == null)
            {
                return null;
            }
            else
            {
                using (Stream stream = content.GetContentStream())
                {
                    StreamReader reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync();
                }
            }
        }

        public string Container
        {
            get;
            set;
        }

        public string BaseAddress
        {
            get { return _baseAddress; }
            set { _baseAddress = value.TrimEnd('/') + '/'; }
        }

        public bool Verbose
        {
            get;
            set;
        }

        public int SaveCount
        {
            get;
            protected set;
        }

        public int LoadCount
        {
            get;
            protected set;
        }

        public int DeleteCount
        {
            get;
            protected set;
        }

        public void ResetStatistics()
        {
            SaveCount = 0;
            LoadCount = 0;
            DeleteCount = 0;
        }

        protected static string GetName(Uri uri, string baseAddress, string container)
        {
            string address = string.Format("{0}{1}/", baseAddress, container);
            string s = uri.ToString();
            string name = s.Substring(address.Length);
            return name;
        }
    }
}

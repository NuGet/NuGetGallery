using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class Storage
    {
        public Storage(Uri baseAddress)
        {
            string s = baseAddress.OriginalString.TrimEnd('/') + '/';
            BaseAddress = new Uri(s);
        }

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

        public Uri BaseAddress { get; private set; }
        
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

        public Uri ResolveUri(string relativeUri)
        {
            return new Uri(BaseAddress, relativeUri);
        }

        protected string GetName(Uri uri)
        {
            string address = BaseAddress.GetLeftPart(UriPartial.Path);
            if (!address.EndsWith("/"))
            {
                address += "/";
            }
            string s = uri.ToString();
            string name = s.Substring(address.Length);
            return name;
        }
    }
}

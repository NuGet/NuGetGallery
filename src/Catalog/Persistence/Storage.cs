using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class Storage
    {
        string _baseAddress;

        public abstract Task Save(string contentType, Uri resourceUri, string content);
        public abstract Task<string> Load(Uri resourceUri);
        public abstract Task Delete(Uri resourceUri);

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

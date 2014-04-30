using System.Threading.Tasks;

namespace Catalog.Persistence
{
    public abstract class Storage
    {
        string _baseAddress;

        public abstract Task Save(string contentType, string name, string content);
        public abstract Task<string> Load(string name);

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

        public void ResetStatistics()
        {
            SaveCount = 0;
            LoadCount = 0;
        }
    }
}

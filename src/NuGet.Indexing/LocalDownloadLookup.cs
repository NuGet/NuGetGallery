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

        protected override string LoadJson()
        {
            using (StreamReader textReader = new StreamReader(Path))
            {
                return textReader.ReadToEnd();
            }
        }
    }
}
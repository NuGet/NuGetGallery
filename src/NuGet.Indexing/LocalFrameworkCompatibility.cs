using Newtonsoft.Json.Linq;
using System.IO;

namespace NuGet.Indexing
{
    public class LocalFrameworkCompatibility : FrameworkCompatibility
    {
        string _path;

        public override string Path { get { return _path; } }

        public LocalFrameworkCompatibility(string path)
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

        public static string GetFileName(string folder)
        {
            return folder.Trim('\\') + "\\data\\" + FileName;
        }
    }
}

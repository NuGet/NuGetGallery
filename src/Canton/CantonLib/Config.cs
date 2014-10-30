using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class Config
    {
        private readonly JObject _config;

        public Config(string path)
        {
            FileInfo file = new FileInfo(path);

            if (!file.Exists)
            {
                throw new FileNotFoundException(file.FullName);
            }

            using (var stream = file.OpenText())
            {
                _config = JObject.Parse(stream.ReadToEnd());
            }
        }

        public string GetProperty(string key)
        {
            JToken token = null;
            if (_config.TryGetValue(key, out token))
            {
                return token.ToString();
            }

            return string.Empty;
        }

        public JObject Json
        {
            get
            {
                return _config;
            }
        }
    }
}

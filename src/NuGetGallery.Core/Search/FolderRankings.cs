using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class FolderRankings : Rankings
    {
        string _folder;

        public FolderRankings(string folder)
        {
            _folder = folder;
        }

        protected override JObject LoadJson()
        {
            string json;
            using (TextReader reader = new StreamReader(_folder.Trim('\\') + "\\all.json"))
            {
                json = reader.ReadToEnd();
            }
            JObject obj = JObject.Parse(json);
            return obj;
        }
    }
}

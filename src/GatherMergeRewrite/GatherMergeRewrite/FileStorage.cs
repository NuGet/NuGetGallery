using JsonLD.Core;
using JsonLDIntegration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading.Tasks;
using VDS.RDF;

namespace GatherMergeRewrite
{
    class FileStorage : IStorage
    {
        public string Path { get; private set; } 

        public FileStorage(string path)
        {
            Path = path.Trim('\\') + '\\';
        }
       
        public async Task Save(string contentType, string name, string content)
        {
            //Console.WriteLine("save {0}", name);

            string folder = string.Empty;

            string[] t = name.Split('/');
            if (t.Length == 2)
            {
                folder = t[0];
                name = t[1];
            }

            if (folder != string.Empty)
            {
                if (!(new DirectoryInfo(Path + folder)).Exists)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(Path);
                    directoryInfo.CreateSubdirectory(folder);
                }

                folder = folder + '\\';
            }

            await Task.Factory.StartNew(() =>
            {
                using (StreamWriter writer = new StreamWriter(Path + folder + name))
                {
                    writer.Write(content);
                }
            });
        }

        public async Task<string> Load(string name)
        {
            //Console.WriteLine("load {0}", name);

            string folder = string.Empty;

            string[] t = name.Split('/');
            if (t.Length == 2)
            {
                folder = t[0];
                name = t[1];
            }

            if (folder != string.Empty)
            {
                folder = folder + '\\';
            }

            string filename = Path + folder + name;

            FileInfo fileInfo = new FileInfo(filename);
            if (fileInfo.Exists)
            {
                using (StreamReader reader = new StreamReader(filename))
                {
                    return await reader.ReadToEndAsync();
                }
            }

            return null;
        }
    }
}

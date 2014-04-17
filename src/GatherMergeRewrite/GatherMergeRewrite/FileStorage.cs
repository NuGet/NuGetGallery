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
        public FileStorage()
        {
            ResetStatistics();
        }

        public string Path
        { 
            get;
            set;
        } 

        public string Container
        {
            get;
            set; 
        }
        
        public string BaseAddress
        { 
            get; 
            set; 
        }

        public bool Verbose
        {
            get;
            set;
        }

        public int SaveCount
        {
            get;
            private set;
        }

        public int LoadCount
        {
            get;
            private set;
        }

        public void ResetStatistics()
        {
            SaveCount = 0;
            LoadCount = 0;
        }

        public async Task Save(string contentType, string name, string content)
        {
            SaveCount++;

            if (Verbose)
            {
                Console.WriteLine("save {0}", name);
            }

            string path = Path.Trim('\\') + '\\';

            string[] t = name.Split('/');

            name = t[t.Length - 1];

            if (t.Length > 1)
            {
                for (int i = 0; i < t.Length - 1; i++)
                {
                    string folder = t[i];

                    if (folder != string.Empty)
                    {
                        if (!(new DirectoryInfo(path + folder)).Exists)
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo(path);
                            directoryInfo.CreateSubdirectory(folder);
                        }

                        path = path + folder + '\\';
                    }
                }
            }

            await Task.Factory.StartNew(() =>
            {
                using (StreamWriter writer = new StreamWriter(path + name))
                {
                    writer.Write(content);
                }
            });
        }

        public async Task<string> Load(string name)
        {
            LoadCount++;

            if (Verbose)
            {
                Console.WriteLine("load {0}", name);
            }

            string path = Path.Trim('\\') + '\\';

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

            string filename = path + folder + name;

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

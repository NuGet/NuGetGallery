using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class FileStorage : Storage
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

        public override async Task Save(string contentType, Uri resourceUri, string content)
        {
            SaveCount++;

            string name = GetName(resourceUri, BaseAddress, Container);

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

        public override async Task<string> Load(Uri resourceUri)
        {
            LoadCount++;

            string name = GetName(resourceUri, BaseAddress, Container);

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

        public override async Task Delete(Uri resourceUri)
        {
            DeleteCount++;

            string name = GetName(resourceUri, BaseAddress, Container);

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
                await Task.Run(() => { fileInfo.Delete(); });
            }
        }
    }
}

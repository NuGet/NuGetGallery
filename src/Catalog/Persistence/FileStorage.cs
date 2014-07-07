using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class FileStorage : Storage
    {
        public FileStorage(string baseAddress, string path) : this(new Uri(baseAddress), path) { }

        public FileStorage(Uri baseAddress, string path)
        {
            Path = path;
            BaseAddress = baseAddress;

            ResetStatistics();
        }

        public string Path
        { 
            get;
            set;
        }

        public override async Task Save(Uri resourceUri, StorageContent content)
        {
            SaveCount++;

            string name = GetName(resourceUri);

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

            using (FileStream stream = File.Create(path + name))
            {
                await content.GetContentStream().CopyToAsync(stream);
            }
        }

        public override async Task<StorageContent> Load(Uri resourceUri)
        {
            LoadCount++;

            string name = GetName(resourceUri);

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
                return await Task.Run<StorageContent>(() => { return new StreamStorageContent(fileInfo.OpenRead()); });
            }

            return null;
        }

        public override async Task Delete(Uri resourceUri)
        {
            DeleteCount++;

            string name = GetName(resourceUri);

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

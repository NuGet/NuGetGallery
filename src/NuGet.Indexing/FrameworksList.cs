using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public abstract class FrameworksList
    {
        public static readonly FrameworkName AnyFramework = new FrameworkName("Any", new Version(0, 0));
        public static readonly string FileName = "projectframeworks.v1.json";

        public static FrameworksList Empty = new EmptyFrameworksList();

        public abstract string Path { get; }
        protected abstract JObject LoadJson();

        public IList<FrameworkName> Load()
        {
            JObject obj = LoadJson();
            if (obj == null)
            {
                return new List<FrameworkName>();
            }
            var data = obj.Value<JArray>("data");
            var list = data.Select(t => new FrameworkName(t.ToString())).ToList();
            list.Add(FrameworksList.AnyFramework);
            return list;
        }

        private class EmptyFrameworksList : FrameworksList
        {
            public override string Path
            {
                get { return "<empty>"; }
            }

            protected override JObject LoadJson()
            {
                return null;
            }
        }
    }

    public class LocalFrameworksList : FrameworksList
    {
        string _path;

        public override string Path { get { return _path; } }

        public LocalFrameworksList(string path)
        {
            _path = path;
        }

        protected override JObject LoadJson()
        {
            if (!File.Exists(Path))
            {
                return null;
            }

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

    public class StorageFrameworksList : FrameworksList
    {
        CloudBlockBlob _blob;
        private CloudStorageAccount storageAccount;
        private string frameworksContainer;
        private string path;

        public override string Path { get { return _blob.Uri.AbsoluteUri; } }

        public StorageFrameworksList(string connectionString)
            : this(CloudStorageAccount.Parse(connectionString))
        {
        }

        public StorageFrameworksList(CloudStorageAccount storageAccount)
            : this(storageAccount, "ng-search")
        {
        }

        public StorageFrameworksList(CloudStorageAccount storageAccount, string containerName)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName))
        {
        }

        public StorageFrameworksList(CloudBlobContainer container)
            : this(container.GetBlockBlobReference(@"data/" + FileName))
        {
        }

        public StorageFrameworksList(CloudBlockBlob blob)
        {
            _blob = blob;
        }

        public StorageFrameworksList(CloudStorageAccount storageAccount, string containerName, string path)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName).GetBlockBlobReference(path))
        {
        }

        protected override JObject LoadJson()
        {
            if (!_blob.Exists())
            {
                return null;
            }
            string json = _blob.DownloadText();
            JObject obj = JObject.Parse(json);
            return obj;
        }
    }
}

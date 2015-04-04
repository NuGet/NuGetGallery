using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;

namespace NuGet.Indexing
{
    public abstract class IndexTask
    {
        public abstract void Execute();

        public TextWriter Log { get; set; }

        public CloudStorageAccount StorageAccount { get; set; }
        public string Container { get; set; }
        public string DataContainer { get; set; }
        public string Folder { get; set; }
        public string FrameworksFile { get; set; }
        public string SqlConnectionString { get; set; }
        public bool WhatIf { get; set; }

        public IndexTask()
        {
            Log = Console.Out;
        }

        protected PackageSearcherManager GetSearcherManager()
        {
            PackageSearcherManager manager;
            if (!string.IsNullOrEmpty(Container))
            {
                manager = PackageSearcherManager.CreateAzure(
                    StorageAccount,
                    Container,
                    DataContainer);
            }
            else if (!string.IsNullOrEmpty(Folder))
            {
                manager = PackageSearcherManager.CreateLocal(
                    Folder,
                    FrameworksFile);
            }
            else
            {
                throw new Exception("You must specify either a folder or container");
            }

            manager.Open();
            return manager;
        }

        public FrameworksList GetFrameworksList()
        {
            if (!String.IsNullOrEmpty(Folder))
            {
                if (String.IsNullOrEmpty(FrameworksFile))
                {
                    FrameworksFile = Path.Combine(Folder, "data", FrameworksList.FileName);
                }
                return new LocalFrameworksList(FrameworksFile);
            }
            else
            {
                string dataPath = String.Empty;
                if (String.IsNullOrEmpty(DataContainer))
                {
                    DataContainer = Container;
                    dataPath = "data/";
                }
                return new StorageFrameworksList(StorageAccount, DataContainer, dataPath + FrameworksList.FileName);
            }
        }

        public Lucene.Net.Store.Directory GetDirectory()
        {
            if (!String.IsNullOrEmpty(Folder))
            {
                return new SimpleFSDirectory(new DirectoryInfo(Folder));
            }
            else
            {
                return new AzureDirectory(StorageAccount, Container, new RAMDirectory());
            }
        }
    }
}

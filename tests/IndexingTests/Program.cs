using Lucene.Net.Store;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Indexing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexingTests
{
    class Program
    {
        static string SqlConnectionString;
        static string AccountName;
        static string KeyValue;
        static string Path;

        static void Main(string[] args)
        {
            SqlConnectionString = args[0];
            AccountName = args[1];         
            KeyValue = args[2];
            Path = args[3];

            try
            {
                DirectoryInfo path = new DirectoryInfo(Path);
                if (!path.Exists)
                {
                    path.Create();
                }

                CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials(AccountName, KeyValue), true);
                FrameworksList frameworksList = new StorageFrameworksList(storageAccount, "ng-search-data", FrameworksList.FileName);
                Lucene.Net.Store.Directory directory = new SimpleFSDirectory(path);

                PackageIndexing.RebuildIndex(SqlConnectionString, directory, frameworksList, Console.Out);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}

using System;
using System.IO;
using Lucene.Net.Store;
using NuGet.Indexing;

namespace Db2Lucene
{
    class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage: db2lucene.exe <sql db connection string> <target local folder> [<true|false>]");
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return;
            }

            var sqlConn = args[0];
            var localPath = args[1];
            var includeUnlisted = (args.Length > 2 ? bool.Parse(args[3]) : false);
            
            if (!System.IO.Directory.Exists(localPath))
            {
                System.IO.Directory.CreateDirectory(localPath);
            }

            var directory = new SimpleFSDirectory(new DirectoryInfo(localPath));
            PackageIndexing.RebuildIndex(sqlConn, directory, Console.Out, null, includeUnlisted: includeUnlisted);
        }
    }
}

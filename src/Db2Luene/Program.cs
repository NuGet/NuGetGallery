using Lucene.Net.Store;
using NuGet.Indexing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Db2Luene
{
    class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage: db2lucene.exe <sql db connection string> <target local folder>");
        }

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                PrintUsage();
                return;
            }

            var sqlConn = args[0];
            var localPath = args[1];

            if (!System.IO.Directory.Exists(localPath))
            {
                System.IO.Directory.CreateDirectory(localPath);
            }

            var directory = new SimpleFSDirectory(new DirectoryInfo(localPath));
            PackageIndexing.RebuildIndex(sqlConn, directory, Console.Out, null);
        }
    }
}

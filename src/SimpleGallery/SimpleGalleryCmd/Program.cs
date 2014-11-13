using Microsoft.WindowsAzure.Storage;
using SimpleGalleryLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleGalleryCmd
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("SimpleGalleryCmd.exe <nupkg path> <azure storage conn string>");
                Environment.Exit(1);
            }

            FileInfo file = new FileInfo(args[0]);

            if (!file.Exists)
            {
                throw new FileNotFoundException(file.FullName);
            }

            CloudStorageAccount account = CloudStorageAccount.Parse(args[1]);

            Console.WriteLine("Adding {0} to {1}", file.Name, account.BlobStorageUri.PrimaryUri.AbsoluteUri);

            SimpleGalleryAPI.AddPackage(account, file.OpenRead());

            Console.WriteLine("Done");
        }
    }
}

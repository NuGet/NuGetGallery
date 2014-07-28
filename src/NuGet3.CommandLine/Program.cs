using NuGet3.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

using NuGet3.Client.Core;

namespace NuGet3.CommandLine
{
    class Program
    {
        static PackageSources _sources;

        static Program()
        {
            _sources = new PackageSources();
            _sources.Initialize();
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return;
            }

            switch(args[0])
            {
                case "install":
                    Commands.Install(_sources, args[1]).Wait();
                    break;
                case "restore":
                    Commands.Restore(_sources).Wait();
                    break;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  nuget3 install <Id> [Version]");
            Console.WriteLine("  nuget3 restore");
        }
    }
}

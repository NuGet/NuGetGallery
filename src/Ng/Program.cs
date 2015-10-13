// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Ng
{
    class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage: ng [package2catalog|feed2catalog|catalog2registration|catalog2lucene|catalog2dnx|frameworkcompatibility|copylucene|checklucene|clearlucene]");
        }

        static void Main(string[] args)
        {
            if (args.Length > 0 && String.Equals("dbg", args[0], StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            try
            {
                if (args.Length == 0)
                {
                    PrintUsage();
                    return;
                }

                switch (args[0])
                {
                    case "package2catalog":
                        var packageToCatalog = new Feed2Catalog();
                        packageToCatalog.Package(args, cancellationTokenSource.Token);
                        break;
                    case "feed2catalog":
                        var feedToCatalog = new Feed2Catalog();
                        feedToCatalog.Run(args, cancellationTokenSource.Token);
                        break;
                    case "catalog2registration":
                        var catalog2Registration = new Catalog2Registration();
                        catalog2Registration.Run(args, cancellationTokenSource.Token);
                        break;
                    case "catalog2lucene":
                        Catalog2Lucene.Run(args, cancellationTokenSource.Token);
                        break;
                    case "catalog2dnx":
                        var catalogToDnx = new Catalog2Dnx();
                        catalogToDnx.Run(args, cancellationTokenSource.Token);
                        break;
                    case "frameworkcompatibility":
                        FrameworkCompatibility.Run(args);
                        break;
                    case "copylucene":
                        CopyLucene.Run(args);
                        break;
                    case "checklucene":
                        CheckLucene.Run(args);
                        break;
                    case "clearlucene":
                        ResetLucene.Run(args);
                        break;
                    default:
                        PrintUsage();
                        break;
                }
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
            }
            Trace.Close();
        }
    }
}

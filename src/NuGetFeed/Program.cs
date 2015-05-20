// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    public class Program
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            if (args.Length > 0 && String.Equals("dbg", args[0], StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            try
            {
                if (args.Length == 0)
                {
                    ArgUsage.GetStyledUsage<Commands>().Write();
                }
                else
                {
                    Args.InvokeAction<Commands>(args);
                }
            }
            catch (ArgException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}

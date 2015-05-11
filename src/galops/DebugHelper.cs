// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace NuGetGallery.Operations.Tools
{
    public static class DebugHelper
    {
        [Conditional("DEBUG")]
        public static void WaitForDebugger(ref string[] args)
        {
            if (args.Length >= 1 && 
                (String.Equals("dbg", args[0], StringComparison.OrdinalIgnoreCase) || 
                 String.Equals("debug", args[0], StringComparison.OrdinalIgnoreCase)))
            {
                args = args.Skip(1).ToArray();
                Console.WriteLine("Waiting for Debugger...");
                Debugger.Launch();
            }
        }
    }
}

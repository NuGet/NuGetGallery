// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;

namespace Tests.AppConfig.AzureJobTraceListener
{
    class Program
    {
        static void Main()
        {
            Trace.TraceInformation("Program Information");
            Trace.TraceWarning("Program Warning");
            Trace.TraceError("Program error");
            Trace.Close();
        }
    }
}

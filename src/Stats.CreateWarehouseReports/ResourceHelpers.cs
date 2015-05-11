// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stats.CreateWarehouseReports.Helpers
{
    public static class ResourceHelpers
    {
        public static Task<string> ReadResourceFile(string name)
        {
            return ReadResourceFile(name, typeof(ResourceHelpers).Assembly);
        }

        public static async Task<string> ReadResourceFile(string resourceName, Assembly asm)
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(assemblyName + "." + resourceName))
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}

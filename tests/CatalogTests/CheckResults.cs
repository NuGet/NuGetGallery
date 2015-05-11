// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace CatalogTests
{
    public class CheckResults
    {
        public static void Test0()
        {
            JObject obj = JObject.Parse((new StreamReader(@"C:\data\site\test\resolver\test.metadata.service.json")).ReadToEnd());

            JArray packages = (JArray)obj["package"];

            Console.WriteLine(packages.Count);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;

namespace NuGet.Indexing
{
    public class LocalFrameworksList : FrameworksList
    {
        private readonly string _path;

        public override string Path { get { return _path; } }

        public LocalFrameworksList(string path)
        {
            _path = path;
        }

        protected override JObject LoadJson()
        {
            if (!File.Exists(Path))
            {
                return null;
            }

            string json;
            using (TextReader reader = new StreamReader(Path))
            {
                json = reader.ReadToEnd();
            }
            JObject obj = JObject.Parse(json);
            return obj;
        }

        public static string GetFileName(string folder)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}\\data\\{1}", folder.Trim('\\'), FileName);
        }
    }
}
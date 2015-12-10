// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System.IO;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public class LocalDownloadLookup : DownloadLookup
    {
        private readonly string _path;

        public override string Path { get { return _path; } }

        public LocalDownloadLookup(string path, FrameworkLogger logger)
            : base(logger)
        {
            _path = path;
        }

        protected override JsonReader GetReader()
        {
            return new JsonTextReader(new StreamReader(File.OpenRead(Path)));
        }
    }
}